using Azure;
using Azure.Data.Tables;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WeddingApi.Domain;

namespace WeddingApi.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string GroupsTableName { get; init; } = "RsvpGroups";
    public string ResponsesTableName { get; init; } = "RsvpResponses";
    public string SheetExportsTableName { get; init; } = "RsvpSheetExports";
    public string DeviceClaimsTableName { get; init; } = "RsvpDeviceClaims";
}

public sealed record GroupState(
    ConfirmationStatus Status,
    string? LockSessionId,
    DateTimeOffset? LockExpiresAtUtc,
    DateTimeOffset? ConfirmedAtUtc,
    GroupEventResponse? EventResponse,
    ETag ETag
);

public interface IRsvpStorage
{
    Task<GroupState> GetOrCreateGroupAsync(string groupId, CancellationToken cancellationToken);
    Task<GroupState> ClaimGroupAsync(string groupId, string deviceId, TimeSpan lockDuration, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, RsvpPersonEntity>> GetResponsesAsync(string groupId, CancellationToken cancellationToken);
    Task SubmitAsync(
        string groupId,
        string lockSessionId,
        GroupEventResponse eventResponse,
        IReadOnlyList<RsvpPersonEntity> personEntities,
        CancellationToken cancellationToken);
    Task ResetGroupAsync(string groupId, CancellationToken cancellationToken);
}

public sealed class TableStorageRepository : IRsvpStorage
{
    private readonly TableServiceClient _serviceClient;
    private readonly StorageOptions _options;

    public TableStorageRepository(TableServiceClient serviceClient, IOptions<StorageOptions> options)
    {
        _serviceClient = serviceClient;
        _options = options.Value;
    }

    private TableClient GroupsTable => _serviceClient.GetTableClient(_options.GroupsTableName);
    private TableClient ResponsesTable => _serviceClient.GetTableClient(_options.ResponsesTableName);
    private TableClient DeviceClaimsTable => _serviceClient.GetTableClient(_options.DeviceClaimsTableName);

    public async Task<GroupState> GetOrCreateGroupAsync(string groupId, CancellationToken cancellationToken)
    {
        await GroupsTable.CreateIfNotExistsAsync(cancellationToken);

        try
        {
            var response = await GroupsTable.GetEntityAsync<RsvpGroupEntity>(groupId, "meta", cancellationToken: cancellationToken);
            return ToState(response.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            var entity = new RsvpGroupEntity { PartitionKey = groupId };
            await GroupsTable.AddEntityAsync(entity, cancellationToken);
            // Fetch again to get ETag
            var response = await GroupsTable.GetEntityAsync<RsvpGroupEntity>(groupId, "meta", cancellationToken: cancellationToken);
            return ToState(response.Value);
        }
    }

    public async Task<GroupState> ClaimGroupAsync(string groupId, string deviceId, TimeSpan lockDuration, CancellationToken cancellationToken)
    {
        await GroupsTable.CreateIfNotExistsAsync(cancellationToken);
        await DeviceClaimsTable.CreateIfNotExistsAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        // Enforce "only one active claim per device": if this device had another claim, release it.
        await ReleasePreviousClaimIfAnyAsync(deviceId, groupId, now, cancellationToken);

        var group = await GetOrCreateGroupAsync(groupId, cancellationToken);

        if (group.Status == ConfirmationStatus.Confirmed)
        {
            return group with { LockSessionId = null };
        }

        var isExpired = group.Status == ConfirmationStatus.Locked && group.LockExpiresAtUtc is not null && group.LockExpiresAtUtc <= now;
        if (group.Status == ConfirmationStatus.Locked && !isExpired)
        {
            // Locked by someone else (or this same device).
            if (string.Equals(group.LockSessionId, deviceId, StringComparison.Ordinal))
            {
                // Same device: keep it valid and extend the lock.
                var extended = await UpsertGroupLockAsync(groupId, deviceId, now.Add(lockDuration), group, cancellationToken);
                await UpsertDeviceClaimAsync(deviceId, groupId, extended.LockExpiresAtUtc, cancellationToken);
                return extended;
            }

            // Someone else: don't leak their device id/session id.
            return group with { LockSessionId = null };
        }

        var updatedState = await UpsertGroupLockAsync(groupId, deviceId, now.Add(lockDuration), group, cancellationToken);
        await UpsertDeviceClaimAsync(deviceId, groupId, updatedState.LockExpiresAtUtc, cancellationToken);
        return updatedState;
    }

    private async Task ReleasePreviousClaimIfAnyAsync(
        string deviceId,
        string newGroupId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var previous = await TryGetDeviceClaimAsync(deviceId, cancellationToken);
        if (previous is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(previous.GroupId) || string.Equals(previous.GroupId, newGroupId, StringComparison.Ordinal))
        {
            return;
        }

        // Best-effort unlock old group so other devices don't have to wait.
        await TryReleaseGroupLockAsync(previous.GroupId, deviceId, now, cancellationToken);
    }

    private async Task<RsvpDeviceClaimEntity?> TryGetDeviceClaimAsync(string deviceId, CancellationToken cancellationToken)
    {
        try
        {
            var res = await DeviceClaimsTable.GetEntityAsync<RsvpDeviceClaimEntity>(deviceId, "claim", cancellationToken: cancellationToken);
            return res.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private async Task UpsertDeviceClaimAsync(string deviceId, string groupId, DateTimeOffset? expiresAtUtc, CancellationToken cancellationToken)
    {
        var entity = new RsvpDeviceClaimEntity
        {
            PartitionKey = deviceId,
            RowKey = "claim",
            GroupId = groupId,
            ExpiresAtUtc = expiresAtUtc,
        };

        await DeviceClaimsTable.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    private async Task TryClearDeviceClaimAsync(string deviceId, string groupId, CancellationToken cancellationToken)
    {
        var claim = await TryGetDeviceClaimAsync(deviceId, cancellationToken);
        if (claim is null)
        {
            return;
        }

        if (!string.Equals(claim.GroupId, groupId, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            await DeviceClaimsTable.DeleteEntityAsync(claim.PartitionKey, claim.RowKey, claim.ETag, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
        }
    }

    private async Task TryReleaseGroupLockAsync(
        string groupId,
        string deviceId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            var res = await GroupsTable.GetEntityAsync<RsvpGroupEntity>(groupId, "meta", cancellationToken: cancellationToken);
            var entity = res.Value;

            var status = Enum.TryParse<ConfirmationStatus>(entity.Status, out var s) ? s : ConfirmationStatus.Open;
            if (status == ConfirmationStatus.Confirmed)
            {
                return;
            }

            if (!string.Equals(entity.Status, ConfirmationStatus.Locked.ToString(), StringComparison.Ordinal))
            {
                return;
            }

            if (!string.Equals(entity.LockSessionId, deviceId, StringComparison.Ordinal))
            {
                return;
            }

            // If it's already expired, clearing is harmless (and helps other clients).
            entity.Status = ConfirmationStatus.Open.ToString();
            entity.LockSessionId = null;
            entity.LockExpiresAtUtc = null;
            await GroupsTable.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            // Concurrency; best-effort only.
        }
    }

    private async Task<GroupState> UpsertGroupLockAsync(
        string groupId,
        string deviceId,
        DateTimeOffset lockExpiresAtUtc,
        GroupState current,
        CancellationToken cancellationToken)
    {
        // Update with ETag
        var entity = new RsvpGroupEntity
        {
            PartitionKey = groupId,
            RowKey = "meta",
            ETag = current.ETag,
            Status = ConfirmationStatus.Locked.ToString(),
            LockSessionId = deviceId,
            LockExpiresAtUtc = lockExpiresAtUtc,
            ConfirmedAtUtc = current.ConfirmedAtUtc,
            DinnerAttendance = current.EventResponse?.DinnerAttendance?.ToString(),
            EveningPartyAttendance = current.EventResponse?.EveningPartyAttendance?.ToString(),
            DinnerSingleAttendeePersonId = current.EventResponse?.DinnerSingleAttendeePersonId,
            EveningPartySingleAttendeePersonId = current.EventResponse?.EveningPartySingleAttendeePersonId,
            DinnerAttendeePersonIds = SerializeIds(current.EventResponse?.DinnerAttendeePersonIds),
            EveningPartyAttendeePersonIds = SerializeIds(current.EventResponse?.EveningPartyAttendeePersonIds),
        };

        try
        {
            await GroupsTable.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            // Concurrency: re-read and retry once.
            var latest = await GetOrCreateGroupAsync(groupId, cancellationToken);
            entity.ETag = latest.ETag;
            await GroupsTable.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);
        }

        var updated = await GroupsTable.GetEntityAsync<RsvpGroupEntity>(groupId, "meta", cancellationToken: cancellationToken);
        return ToState(updated.Value);
    }

    public async Task<IReadOnlyDictionary<string, RsvpPersonEntity>> GetResponsesAsync(string groupId, CancellationToken cancellationToken)
    {
        await ResponsesTable.CreateIfNotExistsAsync(cancellationToken);

        var result = new Dictionary<string, RsvpPersonEntity>(StringComparer.Ordinal);
        await foreach (var entity in ResponsesTable.QueryAsync<RsvpPersonEntity>(e => e.PartitionKey == groupId, cancellationToken: cancellationToken))
        {
            result[entity.RowKey] = entity;
        }

        return result;
    }

    public async Task SubmitAsync(
        string groupId,
        string lockSessionId,
        GroupEventResponse eventResponse,
        IReadOnlyList<RsvpPersonEntity> personEntities,
        CancellationToken cancellationToken)
    {
        await GroupsTable.CreateIfNotExistsAsync(cancellationToken);
        await ResponsesTable.CreateIfNotExistsAsync(cancellationToken);
        await DeviceClaimsTable.CreateIfNotExistsAsync(cancellationToken);

        var groupEntityResponse = await GroupsTable.GetEntityAsync<RsvpGroupEntity>(groupId, "meta", cancellationToken: cancellationToken);
        var group = groupEntityResponse.Value;

        var now = DateTimeOffset.UtcNow;
        if (!string.Equals(group.Status, ConfirmationStatus.Locked.ToString(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Group is not locked.");
        }

        if (!string.Equals(group.LockSessionId, lockSessionId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid lock session.");
        }

        if (group.LockExpiresAtUtc is not null && group.LockExpiresAtUtc <= now)
        {
            throw new InvalidOperationException("Lock expired.");
        }

        // Upsert all person entities (best-effort; cross-table transaction not possible)
        foreach (var person in personEntities)
        {
            person.PartitionKey = groupId;
            person.UpdatedAtUtc = now;
            await ResponsesTable.UpsertEntityAsync(person, TableUpdateMode.Replace, cancellationToken);
        }

        group.Status = ConfirmationStatus.Confirmed.ToString();
        group.ConfirmedAtUtc = now;
        group.LockSessionId = null;
        group.LockExpiresAtUtc = null;
        group.DinnerAttendance = eventResponse.DinnerAttendance?.ToString();
        group.EveningPartyAttendance = eventResponse.EveningPartyAttendance?.ToString();
        group.DinnerSingleAttendeePersonId = eventResponse.DinnerSingleAttendeePersonId;
        group.EveningPartySingleAttendeePersonId = eventResponse.EveningPartySingleAttendeePersonId;
        group.DinnerAttendeePersonIds = SerializeIds(eventResponse.DinnerAttendeePersonIds);
        group.EveningPartyAttendeePersonIds = SerializeIds(eventResponse.EveningPartyAttendeePersonIds);

        await GroupsTable.UpdateEntityAsync(group, group.ETag, TableUpdateMode.Replace, cancellationToken);

        // Clear the device claim so the device can claim another group without waiting.
        await TryClearDeviceClaimAsync(lockSessionId, groupId, cancellationToken);
    }

    public async Task ResetGroupAsync(string groupId, CancellationToken cancellationToken)
    {
        await GroupsTable.CreateIfNotExistsAsync(cancellationToken);
        await ResponsesTable.CreateIfNotExistsAsync(cancellationToken);

        // Delete all responses
        var toDelete = new List<RsvpPersonEntity>();
        await foreach (var entity in ResponsesTable.QueryAsync<RsvpPersonEntity>(e => e.PartitionKey == groupId, cancellationToken: cancellationToken))
        {
            toDelete.Add(entity);
        }

        foreach (var entity in toDelete)
        {
            try
            {
                await ResponsesTable.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, entity.ETag, cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
            }
        }

        // Reset group meta
        var groupEntityResponse = await GroupsTable.GetEntityAsync<RsvpGroupEntity>(groupId, "meta", cancellationToken: cancellationToken);
        var group = groupEntityResponse.Value;
        group.Status = ConfirmationStatus.Open.ToString();
        group.LockSessionId = null;
        group.LockExpiresAtUtc = null;
        group.ConfirmedAtUtc = null;
        group.DinnerAttendance = null;
        group.EveningPartyAttendance = null;
        group.DinnerSingleAttendeePersonId = null;
        group.EveningPartySingleAttendeePersonId = null;
        group.DinnerAttendeePersonIds = null;
        group.EveningPartyAttendeePersonIds = null;

        await GroupsTable.UpdateEntityAsync(group, group.ETag, TableUpdateMode.Replace, cancellationToken);
    }

    private static GroupState ToState(RsvpGroupEntity entity)
    {
        var status = Enum.TryParse<ConfirmationStatus>(entity.Status, out var s) ? s : ConfirmationStatus.Open;

        GroupEventResponse? eventResponse = null;
        if (entity.DinnerAttendance is not null || entity.EveningPartyAttendance is not null ||
            entity.DinnerSingleAttendeePersonId is not null || entity.EveningPartySingleAttendeePersonId is not null ||
            entity.DinnerAttendeePersonIds is not null || entity.EveningPartyAttendeePersonIds is not null)
        {
            eventResponse = new GroupEventResponse(
                DinnerAttendance: Enum.TryParse<EventAttendance>(entity.DinnerAttendance, out var d) ? d : null,
                EveningPartyAttendance: Enum.TryParse<EventAttendance>(entity.EveningPartyAttendance, out var e) ? e : null,
                DinnerSingleAttendeePersonId: entity.DinnerSingleAttendeePersonId,
                EveningPartySingleAttendeePersonId: entity.EveningPartySingleAttendeePersonId,
                DinnerAttendeePersonIds: DeserializeIds(entity.DinnerAttendeePersonIds),
                EveningPartyAttendeePersonIds: DeserializeIds(entity.EveningPartyAttendeePersonIds));
        }

        return new GroupState(
            Status: status,
            LockSessionId: entity.LockSessionId,
            LockExpiresAtUtc: entity.LockExpiresAtUtc,
            ConfirmedAtUtc: entity.ConfirmedAtUtc,
            EventResponse: eventResponse,
            ETag: entity.ETag);
    }

    private static string? SerializeIds(IReadOnlyList<string>? ids)
    {
        if (ids is null || ids.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(ids);
    }

    private static IReadOnlyList<string>? DeserializeIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json);
        }
        catch
        {
            return null;
        }
    }
}
