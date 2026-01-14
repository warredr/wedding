using System.Net;
using Microsoft.Extensions.Options;
using WeddingApi.Application;
using WeddingApi.Application.Session;
using WeddingApi.Functions;
using WeddingApi.Invites;
using WeddingApi.Storage;

namespace wedding_api.Tests;

public sealed class RsvpFunctionsApiTests
{
    private sealed class StubInvites : IInviteRepository
    {
        public Task<IReadOnlyList<PersonSearchHit>> SearchPeopleAsync(string query, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PersonSearchHit>>(Array.Empty<PersonSearchHit>());

        public Task<WeddingApi.Domain.GroupDefinition?> GetGroupAsync(string groupId, CancellationToken cancellationToken)
            => Task.FromResult<WeddingApi.Domain.GroupDefinition?>(null);

        public Task<IReadOnlyList<WeddingApi.Domain.GroupDefinition>> GetAllGroupsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<WeddingApi.Domain.GroupDefinition>>(Array.Empty<WeddingApi.Domain.GroupDefinition>());
    }

    private sealed class StubStorage : IRsvpStorage
    {
        public Task<GroupState> GetOrCreateGroupAsync(string groupId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<GroupState> ClaimGroupAsync(string groupId, string deviceId, TimeSpan lockDuration, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<IReadOnlyDictionary<string, RsvpPersonEntity>> GetResponsesAsync(string groupId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task SubmitAsync(string groupId, string lockSessionId, WeddingApi.Domain.GroupEventResponse eventResponse, IReadOnlyList<RsvpPersonEntity> personEntities, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task ResetGroupAsync(string groupId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<IReadOnlyDictionary<string, RsvpPersonEntity>> GetAllResponsesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<string, RsvpPersonEntity>>(new Dictionary<string, RsvpPersonEntity>());

        public Task<IReadOnlyDictionary<string, GroupState>> GetAllGroupStatesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<string, GroupState>>(new Dictionary<string, GroupState>());
    }

    private sealed class StubOutbox : ISheetExportOutbox
    {
        public Task EnqueueUpsertAsync(string groupId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task EnqueueDeleteAsync(string groupId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<SheetExportWorkItem>> GetPendingAsync(int maxItems, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SheetExportWorkItem>>(Array.Empty<SheetExportWorkItem>());
        public Task MarkSucceededAsync(string groupId, Azure.ETag etag, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task MarkFailedAsync(string groupId, Azure.ETag etag, string error, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static RsvpFunctions CreateSut(RsvpOptions options)
    {
        var access = new AccessGate(Options.Create(options));
        return new RsvpFunctions(access, Options.Create(options), new StubInvites(), new StubStorage(), new StubOutbox());
    }

    private static RsvpOptions CreateOptions()
    {
        return new RsvpOptions
        {
            QrAccessKey = Guid.NewGuid().ToString(),
            SixDigitAccessCode = "662026",
            SessionSigningKey = "test-signing-key-123",
            SessionTtlMinutes = 120,
            AdminKey = Guid.NewGuid().ToString(),
            DeadlineDate = new DateOnly(2026, 5, 1),
            LockMinutes = 15,
            AllergiesTextMaxLength = 200,
        };
    }

    [Fact]
    public async Task SessionStart_ValidCode_SetsCookie()
    {
        var options = CreateOptions();
        var sut = CreateSut(options);

        var ctx = new TestFunctionContext();
        var req = new TestHttpRequestData(ctx, new Uri("http://localhost/api/session/start"), "POST", "{\"code\":\"662026\"}");

        var res = await sut.SessionStart(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True(res.Headers.TryGetValues("Set-Cookie", out var cookieValues));
        var cookie = cookieValues.First();
        Assert.Contains("rsvp_session=", cookie, StringComparison.Ordinal);
        Assert.Contains("HttpOnly", cookie, StringComparison.Ordinal);

        var body = ((TestHttpResponseData)res).ReadBodyAsString();
        Assert.Contains("\"ok\":true", body);
    }

    [Fact]
    public async Task SessionStart_InvalidCode_ReturnsForbidden()
    {
        var sut = CreateSut(CreateOptions());

        var ctx = new TestFunctionContext();
        var req = new TestHttpRequestData(ctx, new Uri("http://localhost/api/session/start"), "POST", "{\"code\":\"000000\"}");

        var res = await sut.SessionStart(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);

        var body = ((TestHttpResponseData)res).ReadBodyAsString();
        Assert.Contains("invalid_code", body);
    }

    [Fact]
    public void SessionFromQr_InvalidKey_ReturnsForbidden()
    {
        var sut = CreateSut(CreateOptions());

        var ctx = new TestFunctionContext();
        var req = new TestHttpRequestData(ctx, new Uri("http://localhost/api/session/from-qr?k=wrong"), "GET");

        var res = sut.SessionFromQr(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);

        var body = ((TestHttpResponseData)res).ReadBodyAsString();
        Assert.Contains("invalid_qr_key", body);
    }

    [Fact]
    public void Config_WithoutSession_ReturnsForbidden()
    {
        var sut = CreateSut(CreateOptions());

        var ctx = new TestFunctionContext();
        var req = new TestHttpRequestData(ctx, new Uri("http://localhost/api/config"), "GET");

        var res = sut.GetConfig(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);

        var body = ((TestHttpResponseData)res).ReadBodyAsString();
        Assert.Contains("unauthorized", body);
    }

    [Fact]
    public void Config_WithValidSession_ReturnsDeadline()
    {
        var options = CreateOptions();
        var sut = CreateSut(options);

        var ctx = new TestFunctionContext();
        var req = new TestHttpRequestData(ctx, new Uri("http://localhost/api/config"), "GET");

        var token = SessionTokens.CreateToken(DateTimeOffset.UtcNow.AddMinutes(10), options.SessionSigningKey);
        req.Headers.Add("Cookie", $"{AccessGate.SessionCookieName}={token}");

        var res = sut.GetConfig(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = ((TestHttpResponseData)res).ReadBodyAsString();
        Assert.Contains("deadlineDate", body);
        Assert.Contains("isClosed", body);
    }
}
