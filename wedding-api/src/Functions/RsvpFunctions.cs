using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using WeddingApi.Application;
using WeddingApi.Domain;
using WeddingApi.Invites;
using WeddingApi.Storage;

namespace WeddingApi.Functions;

public sealed class RsvpFunctions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly AccessGate _access;
    private readonly RsvpOptions _options;
    private readonly IInviteRepository _invites;
    private readonly IRsvpStorage _storage;
    private readonly ISheetExportOutbox _sheetOutbox;

    public RsvpFunctions(
        AccessGate access,
        IOptions<RsvpOptions> options,
        IInviteRepository invites,
        IRsvpStorage storage,
        ISheetExportOutbox sheetOutbox)
    {
        _access = access;
        _options = options.Value;
        _invites = invites;
        _storage = storage;
        _sheetOutbox = sheetOutbox;
    }

    [Function("session_from_qr")]
    public HttpResponseData SessionFromQr(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "session/from-qr")] HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var key = query.Get("k");
        if (!_access.IsValidQrKey(key))
        {
            return HttpResults.Error(req, HttpStatusCode.Forbidden, "invalid_qr_key", "Invalid QR key.");
        }

        return _access.IssueSessionCookie(req);
    }

    [Function("session_verify")]
    public HttpResponseData SessionVerify(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "session/verify")] HttpRequestData req)
    {
        var authError = _access.RequireSession(req);
        if (authError is not null)
        {
            return authError;
        }

        _access.TryGetSessionExpiresAtUtc(req, out var expiresAtUtc);

        return HttpResults.Json(req, HttpStatusCode.OK, new
        {
            ok = true,
            expiresAtUtc,
        });
    }

    private sealed record StartSessionRequest(string? Code);

    [Function("session_start")]
    public async Task<HttpResponseData> SessionStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "session/start")] HttpRequestData req)
    {
        StartSessionRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<StartSessionRequest>(req.Body, JsonOptions);
        }
        catch
        {
            return HttpResults.Error(req, HttpStatusCode.BadRequest, "invalid_json", "Invalid JSON body.");
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Code) || !_access.IsValidSixDigitCode(body.Code.Trim()))
        {
            return HttpResults.Error(req, HttpStatusCode.Forbidden, "invalid_code", "Invalid access code.");
        }

        return _access.IssueSessionCookie(req);
    }

    [Function("config")]
    public HttpResponseData GetConfig(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "config")] HttpRequestData req)
    {
        var authError = _access.RequireSession(req);
        if (authError is not null)
        {
            return authError;
        }

        _access.TryGetSessionExpiresAtUtc(req, out var sessionExpiresAtUtc);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var isClosed = today > _options.DeadlineDate;

        return HttpResults.Json(req, HttpStatusCode.OK, new
        {
            deadlineDate = _options.DeadlineDate.ToString("yyyy-MM-dd"),
            isClosed,
            sessionExpiresAtUtc,
        });
    }

    [Function("search")]
    public async Task<HttpResponseData> Search(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "search")] HttpRequestData req)
    {
        var authError = _access.RequireSession(req);
        if (authError is not null)
        {
            return authError;
        }

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var q = query.Get("q") ?? string.Empty;

        var hits = await _invites.SearchPeopleAsync(q, req.FunctionContext.CancellationToken);

        // Join with group status (fetch each group state once).
        var cancellationToken = req.FunctionContext.CancellationToken;
        var groupIds = hits
            .Select(h => h.GroupId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var groupStates = await Task.WhenAll(groupIds.Select(async id =>
        {
            var state = await _storage.GetOrCreateGroupAsync(id, cancellationToken);
            return (GroupId: id, Status: state.Status);
        }));

        var statusByGroupId = groupStates.ToDictionary(x => x.GroupId, x => x.Status, StringComparer.Ordinal);

        var results = new List<object>(hits.Count);
        foreach (var hit in hits)
        {
            statusByGroupId.TryGetValue(hit.GroupId, out var status);
            results.Add(new
            {
                personId = hit.PersonId,
                fullName = hit.FullName,
                groupId = hit.GroupId,
                groupLabelFirstNames = hit.GroupLabelFirstNames,
                groupStatus = status,
            });
        }

        return HttpResults.Json(req, HttpStatusCode.OK, results);
    }

    [Function("claim_group")]
    public async Task<HttpResponseData> ClaimGroup(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "groups/{groupId}/claim")] HttpRequestData req,
        string groupId)
    {
        var authError = _access.RequireSession(req);
        if (authError is not null)
        {
            return authError;
        }

        if (!_access.TryGetDeviceId(req, out var deviceId))
        {
            return HttpResults.Error(req, HttpStatusCode.Forbidden, "unauthorized", "Session required.");
        }

        // Claims should only last 2 minutes.
        var state = await _storage.ClaimGroupAsync(groupId, deviceId, TimeSpan.FromMinutes(2), req.FunctionContext.CancellationToken);

        if (state.Status == ConfirmationStatus.Confirmed)
        {
            return HttpResults.Error(req, HttpStatusCode.Conflict, "conflict", "Group already confirmed.", new { reason = "confirmed" });
        }

        if (state.Status == ConfirmationStatus.Locked && state.LockSessionId is null)
        {
            return HttpResults.Error(req, HttpStatusCode.Conflict, "conflict", "Group is locked.", new
            {
                reason = "locked",
                expiresAtUtc = state.LockExpiresAtUtc,
                secondsLeft = SecondsLeft(state.LockExpiresAtUtc),
            });
        }

        return HttpResults.Json(req, HttpStatusCode.OK, new
        {
            groupStatus = state.Status,
            sessionId = state.LockSessionId,
            lockExpiresAtUtc = state.LockExpiresAtUtc,
        });
    }

    [Function("get_group")]
    public async Task<HttpResponseData> GetGroup(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "groups/{groupId}")] HttpRequestData req,
        string groupId)
    {
        var authError = _access.RequireSession(req);
        if (authError is not null)
        {
            return authError;
        }

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var sessionId = query.Get("sessionId");
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return HttpResults.Error(req, HttpStatusCode.BadRequest, "missing_session_id", "Missing sessionId.");
        }

        if (!_access.TryGetDeviceId(req, out var deviceId) || !string.Equals(sessionId, deviceId, StringComparison.Ordinal))
        {
            return HttpResults.Error(req, HttpStatusCode.Forbidden, "unauthorized", "Invalid session.");
        }

        var group = await _invites.GetGroupAsync(groupId, req.FunctionContext.CancellationToken);
        if (group is null)
        {
            return HttpResults.Error(req, HttpStatusCode.NotFound, "not_found", "Unknown group.");
        }

        var state = await _storage.GetOrCreateGroupAsync(groupId, req.FunctionContext.CancellationToken);
        if (state.Status == ConfirmationStatus.Confirmed)
        {
            return HttpResults.Error(req, HttpStatusCode.Conflict, "conflict", "Group already confirmed.", new { reason = "confirmed" });
        }

        if (state.Status != ConfirmationStatus.Locked || !string.Equals(state.LockSessionId, sessionId, StringComparison.Ordinal))
        {
            var expiresAtUtc = state.LockExpiresAtUtc;
            var reason = state.Status == ConfirmationStatus.Locked && SecondsLeft(expiresAtUtc) > 0 ? "locked" : "lock";
            return HttpResults.Error(req, HttpStatusCode.Conflict, "conflict", "Group lock invalid.", new
            {
                reason,
                expiresAtUtc,
                secondsLeft = SecondsLeft(expiresAtUtc),
            });
        }

        // Existing responses (if any)
        var existing = await _storage.GetResponsesAsync(groupId, req.FunctionContext.CancellationToken);

        var members = group.Members.Select(m =>
        {
            existing.TryGetValue(m.PersonId, out var r);
            return new
            {
                personId = m.PersonId,
                fullName = m.FullName,
                attending = r?.Attending,
                hasAllergies = r is null ? (bool?)null : r.HasAllergies,
                allergiesText = r?.AllergiesText,
            };
        });

        return HttpResults.Json(req, HttpStatusCode.OK, new
        {
            groupId = group.GroupId,
            groupLabelFirstNames = group.GroupLabelFirstNames,
            invitedToEvents = group.InvitedTo,
            groupStatus = state.Status,
            members,
            eventAttendance = state.EventResponse,
        });
    }

    private sealed record SubmitRequest(
        GroupEventResponse EventResponse,
        Dictionary<string, PersonResponse> PersonResponses
    );

    [Function("submit_group")]
    public async Task<HttpResponseData> SubmitGroup(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "groups/{groupId}/submit")] HttpRequestData req,
        string groupId)
    {
        var authError = _access.RequireSession(req);
        if (authError is not null)
        {
            return authError;
        }

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var sessionId = query.Get("sessionId");
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return HttpResults.Error(req, HttpStatusCode.BadRequest, "missing_session_id", "Missing sessionId.");
        }

        if (!_access.TryGetDeviceId(req, out var deviceId) || !string.Equals(sessionId, deviceId, StringComparison.Ordinal))
        {
            return HttpResults.Error(req, HttpStatusCode.Forbidden, "unauthorized", "Invalid session.");
        }

        SubmitRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SubmitRequest>(req.Body, JsonOptions);
        }
        catch
        {
            return HttpResults.Error(req, HttpStatusCode.BadRequest, "invalid_json", "Invalid JSON body.");
        }

        if (body is null)
        {
            return HttpResults.Error(req, HttpStatusCode.BadRequest, "invalid_body", "Missing body.");
        }

        var group = await _invites.GetGroupAsync(groupId, req.FunctionContext.CancellationToken);
        if (group is null)
        {
            return HttpResults.Error(req, HttpStatusCode.NotFound, "not_found", "Unknown group.");
        }

        var submission = new GroupSubmission(body.EventResponse, body.PersonResponses);
        var errors = Validation.ValidateSubmission(group, submission, _options.AllergiesTextMaxLength);
        if (errors.Count > 0)
        {
            return HttpResults.Error(req, HttpStatusCode.BadRequest, "validation_failed", "Validation failed.", errors);
        }

        // Map to storage entities
        var personEntities = group.Members.Select(m =>
        {
            var pr = body.PersonResponses[m.PersonId];
            return new RsvpPersonEntity
            {
                PartitionKey = groupId,
                RowKey = m.PersonId,
                FullName = m.FullName,
                Attending = pr.Attending.ToString(),
                HasAllergies = pr.Attending == AttendingStatus.Yes && pr.HasAllergies == true,
                AllergiesText = pr.Attending == AttendingStatus.Yes && pr.HasAllergies == true ? (pr.AllergiesText ?? string.Empty) : string.Empty,
            };
        }).ToList();

        try
        {
            await _storage.SubmitAsync(groupId, sessionId, body.EventResponse, personEntities, req.FunctionContext.CancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            var state = await _storage.GetOrCreateGroupAsync(groupId, req.FunctionContext.CancellationToken);
            var expiresAtUtc = state.LockExpiresAtUtc;
            var reason = state.Status == ConfirmationStatus.Locked && SecondsLeft(expiresAtUtc) > 0 ? "locked" : "lock";
            return HttpResults.Error(req, HttpStatusCode.Conflict, "conflict", ex.Message, new
            {
                reason,
                expiresAtUtc,
                secondsLeft = SecondsLeft(expiresAtUtc),
            });
        }

        try
        {
            await _sheetOutbox.EnqueueUpsertAsync(groupId, req.FunctionContext.CancellationToken);
        }
        catch
        {
            // Table storage already committed; surface error so operator can notice.
            return HttpResults.Error(req, HttpStatusCode.InternalServerError, "export_enqueue_failed", "Could not enqueue export table write.");
        }

        return HttpResults.Json(req, HttpStatusCode.OK, new { ok = true });
    }

    private static int SecondsLeft(DateTimeOffset? expiresAtUtc)
    {
        if (expiresAtUtc is null)
        {
            return 0;
        }

        var delta = expiresAtUtc.Value - DateTimeOffset.UtcNow;
        if (delta <= TimeSpan.Zero)
        {
            return 0;
        }

        return (int)Math.Ceiling(delta.TotalSeconds);
    }

    [Function("manage_reset_group")]
    public async Task<HttpResponseData> AdminResetGroup(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "manage/groups/{groupId}/reset")] HttpRequestData req,
        string groupId)
    {
        if (!_access.IsAdmin(req))
        {
            return HttpResults.Error(req, HttpStatusCode.Forbidden, "unauthorized", "Admin key required.");
        }

        await _storage.ResetGroupAsync(groupId, req.FunctionContext.CancellationToken);

        try
        {
            await _sheetOutbox.EnqueueDeleteAsync(groupId, req.FunctionContext.CancellationToken);
        }
        catch
        {
            return HttpResults.Error(req, HttpStatusCode.InternalServerError, "export_enqueue_failed", "Could not enqueue export table delete.");
        }

        return HttpResults.Json(req, HttpStatusCode.OK, new { ok = true });
    }
}
