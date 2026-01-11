using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using WeddingApi.Domain;
using WeddingApi.Storage;

namespace wedding_api.Tests;

public sealed class TableStorageIntegrationTests
{
    private static TableServiceClient CreateServiceClient()
    {
        var cs = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException("AzureWebJobsStorage is not set.");
        }

        return new TableServiceClient(cs);
    }

    private static StorageOptions CreateUniqueOptions()
    {
        var suffix = Guid.NewGuid().ToString("N");
        return new StorageOptions
        {
            GroupsTableName = $"RsvpGroupsTest{suffix}",
            ResponsesTableName = $"RsvpResponsesTest{suffix}",
            SheetExportsTableName = $"RsvpSheetExportsTest{suffix}",
            DeviceClaimsTableName = $"RsvpDeviceClaimsTest{suffix}",
        };
    }

    [IntegrationFact]
    public async Task Claim_Submit_Reset_RoundTrip_Works()
    {
        var serviceClient = CreateServiceClient();
        var options = CreateUniqueOptions();

        var repo = new TableStorageRepository(serviceClient, Options.Create(options));

        var groupId = "g1";

        var deviceA = "device-a";
        var deviceB = "device-b";

        // Claim once => lock acquired + session id returned
        var claimed = await repo.ClaimGroupAsync(groupId, deviceA, TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Equal(ConfirmationStatus.Locked, claimed.Status);
        Assert.False(string.IsNullOrWhiteSpace(claimed.LockSessionId));
        Assert.Equal(deviceA, claimed.LockSessionId);

        // Claim again from another device before expiry => locked, but no session id leaked
        var claimed2 = await repo.ClaimGroupAsync(groupId, deviceB, TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Equal(ConfirmationStatus.Locked, claimed2.Status);
        Assert.Null(claimed2.LockSessionId);

        var sessionId = claimed.LockSessionId!;

        // Submit requires correct session
        var eventResponse = new GroupEventResponse(
            DinnerAttendance: EventAttendance.All,
            EveningPartyAttendance: EventAttendance.None,
            DinnerSingleAttendeePersonId: null,
            EveningPartySingleAttendeePersonId: null);

        var people = new List<RsvpPersonEntity>
        {
            new()
            {
                RowKey = "p1",
                FullName = "Alice Example",
                Attending = AttendingStatus.Yes.ToString(),
                HasAllergies = false,
                AllergiesText = string.Empty,
            },
            new()
            {
                RowKey = "p2",
                FullName = "Bob Example",
                Attending = AttendingStatus.No.ToString(),
                HasAllergies = false,
                AllergiesText = string.Empty,
            }
        };

        await repo.SubmitAsync(groupId, sessionId, eventResponse, people, CancellationToken.None);

        var stateAfter = await repo.GetOrCreateGroupAsync(groupId, CancellationToken.None);
        Assert.Equal(ConfirmationStatus.Confirmed, stateAfter.Status);
        Assert.NotNull(stateAfter.EventResponse);
        Assert.Equal(EventAttendance.All, stateAfter.EventResponse!.DinnerAttendance);

        var responses = await repo.GetResponsesAsync(groupId, CancellationToken.None);
        Assert.Equal(2, responses.Count);

        // Reset deletes responses + reopens group
        await repo.ResetGroupAsync(groupId, CancellationToken.None);

        var stateReset = await repo.GetOrCreateGroupAsync(groupId, CancellationToken.None);
        Assert.Equal(ConfirmationStatus.Open, stateReset.Status);

        var responsesAfterReset = await repo.GetResponsesAsync(groupId, CancellationToken.None);
        Assert.Empty(responsesAfterReset);
    }

    [IntegrationFact]
    public async Task Outbox_Enqueue_GetPending_MarkSucceeded_Works()
    {
        var serviceClient = CreateServiceClient();
        var options = CreateUniqueOptions();

        var outbox = new SheetExportOutboxRepository(serviceClient, Options.Create(options));

        await outbox.EnqueueUpsertAsync("g1", CancellationToken.None);
        var pending = await outbox.GetPendingAsync(10, CancellationToken.None);
        Assert.Contains(pending, p => p.GroupId == "g1" && p.Operation == SheetExportOperation.Upsert);

        var item = pending.First(p => p.GroupId == "g1");
        await outbox.MarkSucceededAsync(item.GroupId, item.ETag, CancellationToken.None);

        var pending2 = await outbox.GetPendingAsync(10, CancellationToken.None);
        Assert.DoesNotContain(pending2, p => p.GroupId == "g1");
    }
}
