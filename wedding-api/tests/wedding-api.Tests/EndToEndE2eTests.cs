using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeddingApi.Application;
using WeddingApi.Domain;
using WeddingApi.Functions;
using WeddingApi.Infrastructure.ExportTable;
using WeddingApi.Invites;
using WeddingApi.Storage;

namespace wedding_api.Tests;

public sealed class EndToEndE2eTests
{
    [E2eFact]
    public async Task FullFlow_WithAzurite_AndExportTable_Works()
    {
        var azurite = Environment.GetEnvironmentVariable("AzureWebJobsStorage")!;

        var adminKey = Guid.NewGuid().ToString();
        var qrKey = Guid.NewGuid().ToString();
        const string sixDigit = "662026";

        var groupId = "g_" + Guid.NewGuid().ToString("N");
        var personA = "p_" + Guid.NewGuid().ToString("N");
        var personB = "p_" + Guid.NewGuid().ToString("N");

        var invitesPath = WriteTempInvitesJson(groupId, personA, personB);

        var groupsTable = "RsvpGroupsE2E" + Guid.NewGuid().ToString("N");
        var responsesTable = "RsvpResponsesE2E" + Guid.NewGuid().ToString("N");
        var outboxTable = "RsvpSheetExportsE2E" + Guid.NewGuid().ToString("N");
        var exportTable = "RsvpExportRowsE2E" + Guid.NewGuid().ToString("N");

        var env = new Dictionary<string, string>
        {
            ["AzureWebJobsStorage"] = azurite,

            ["Rsvp__QrAccessKey"] = qrKey,
            ["Rsvp__SixDigitAccessCode"] = sixDigit,
            ["Rsvp__SessionTtlMinutes"] = "120",
            ["Rsvp__SessionSigningKey"] = "e2e-signing-key",
            ["Rsvp__AdminKey"] = adminKey,
            ["Rsvp__DeadlineDate"] = "2026-05-01",
            ["Rsvp__LockMinutes"] = "15",
            ["Rsvp__AllergiesTextMaxLength"] = "200",

            ["Invites__JsonPath"] = invitesPath,

            ["Storage__GroupsTableName"] = groupsTable,
            ["Storage__ResponsesTableName"] = responsesTable,
            ["Storage__SheetExportsTableName"] = outboxTable,

            ["ExportTable__TableName"] = exportTable,
        };

        var repoRoot = FindRepoRoot();
        await using var host = new FunctionsHostFixture(
            workingDirectory: Path.Combine(repoRoot, "wedding-api", "src"),
            environment: env);

        await host.StartAsync(CancellationToken.None);

        var handler = new HttpClientHandler { UseCookies = true, CookieContainer = new System.Net.CookieContainer() };
        using var client = new HttpClient(handler) { BaseAddress = host.BaseUri };

        // 1) Start session
        var sessionRes = await client.PostAsJsonAsync("api/session/start", new { code = sixDigit });
        Assert.Equal(HttpStatusCode.OK, sessionRes.StatusCode);

        // 2) Search
        var searchRes = await client.GetAsync("api/search?q=Alice");
        Assert.Equal(HttpStatusCode.OK, searchRes.StatusCode);

        var searchJson = await searchRes.Content.ReadAsStringAsync();
        Assert.Contains(groupId, searchJson);

        // 3) Claim
        var claimRes = await client.PostAsync($"api/groups/{groupId}/claim", content: null);
        Assert.Equal(HttpStatusCode.OK, claimRes.StatusCode);

        var claim = await claimRes.Content.ReadFromJsonAsync<JsonElement>();
        var lockSessionId = claim.GetProperty("sessionId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(lockSessionId));

        // 4) Get group
        var getGroupRes = await client.GetAsync($"api/groups/{groupId}?sessionId={lockSessionId}");
        Assert.Equal(HttpStatusCode.OK, getGroupRes.StatusCode);

        // 5) Submit
        var payload = new
        {
            eventResponse = new
            {
                dinnerAttendance = "All",
                eveningPartyAttendance = "One",
                dinnerSingleAttendeePersonId = (string?)null,
                eveningPartySingleAttendeePersonId = personA,
            },
            personResponses = new Dictionary<string, object>
            {
                [personA] = new { attending = "Yes", hasAllergies = true, allergiesText = "Peanuts" },
                [personB] = new { attending = "Yes", hasAllergies = false, allergiesText = (string?)null },
            }
        };

        var submitRes = await client.PostAsJsonAsync($"api/groups/{groupId}/submit?sessionId={lockSessionId}", payload);
        Assert.Equal(HttpStatusCode.OK, submitRes.StatusCode);

        // 6) Run export timer once (deterministic) and verify export table contains rows.
        await RunExportTimerOnceAsync(env, groupId);
        await AssertExportTableContainsPersonsAsync(env, groupId, personA, personB);

        // Cleanup best-effort.
        await BestEffortDeleteFromExportTableAsync(env, groupId);

        // 7) Admin reset and reclaim should work.
        var resetRes = await client.PostAsync($"api/admin/groups/{groupId}/reset?adminKey={adminKey}", content: null);
        Assert.Equal(HttpStatusCode.OK, resetRes.StatusCode);

        var claimAgain = await client.PostAsync($"api/groups/{groupId}/claim", content: null);
        Assert.Equal(HttpStatusCode.OK, claimAgain.StatusCode);
    }

    private static string WriteTempInvitesJson(string groupId, string personA, string personB)
    {
        var file = new InvitesFile
        {
            SchemaVersion = 1,
            Groups =
            {
                new InviteGroup
                {
                    GroupId = groupId,
                    GroupLabelFirstNames = "Alice & Bob",
                    InvitedTo = new List<EventType> { EventType.Dinner, EventType.EveningParty },
                    Members = new List<InvitePerson>
                    {
                        new InvitePerson { PersonId = personA, FullName = "Alice Example" },
                        new InvitePerson { PersonId = personB, FullName = "Bob Example" },
                    }
                }
            }
        };

        var path = Path.Combine(Path.GetTempPath(), $"invites-e2e-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(file));
        return path;
    }

    private static async Task RunExportTimerOnceAsync(IDictionary<string, string> env, string groupId)
    {
        var serviceClient = new TableServiceClient(env["AzureWebJobsStorage"]);
        var storageOptions = Options.Create(new StorageOptions
        {
            GroupsTableName = env["Storage__GroupsTableName"],
            ResponsesTableName = env["Storage__ResponsesTableName"],
            SheetExportsTableName = env["Storage__SheetExportsTableName"],
        });

        var invitesOptions = Options.Create(new InvitesOptions { JsonPath = env["Invites__JsonPath"], CacheSeconds = 5 });
        var exportTableOptions = Options.Create(new ExportTableOptions { TableName = env["ExportTable__TableName"] });

        var outbox = new SheetExportOutboxRepository(serviceClient, storageOptions);
        var storage = new TableStorageRepository(serviceClient, storageOptions);
        var invites = new JsonInviteRepository(invitesOptions);
        var exporter = new ExportTableExporter(serviceClient, exportTableOptions);

        var timer = new SheetExportTimer(NullLogger<SheetExportTimer>.Instance, outbox, storage, invites, exporter);

        // Process pending work (submit enqueued one).
        await timer.Run(timer: null!, cancellationToken: CancellationToken.None);

        // Ensure outbox eventually transitions out of pending.
        var pending = await outbox.GetPendingAsync(25, CancellationToken.None);
        Assert.DoesNotContain(pending, p => p.GroupId == groupId && p.Operation == SheetExportOperation.Upsert);
    }

    private static async Task AssertExportTableContainsPersonsAsync(IDictionary<string, string> env, string groupId, string personA, string personB)
    {
        var serviceClient = new TableServiceClient(env["AzureWebJobsStorage"]);
        var table = serviceClient.GetTableClient(env["ExportTable__TableName"]);
        await table.CreateIfNotExistsAsync(CancellationToken.None);

        var escapedGroupId = groupId.Replace("'", "''");
        var entities = new List<ExportRowEntity>();

        await foreach (var entity in table.QueryAsync<ExportRowEntity>(
                           filter: $"PartitionKey eq '{escapedGroupId}'",
                           cancellationToken: CancellationToken.None))
        {
            entities.Add(entity);
        }

        Assert.Contains(entities, e => e.PartitionKey == groupId && e.RowKey == personA);
        Assert.Contains(entities, e => e.PartitionKey == groupId && e.RowKey == personB);

        Assert.Contains(entities, e => e.RowKey == personA && e.Allergies.Contains("Peanuts", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task BestEffortDeleteFromExportTableAsync(IDictionary<string, string> env, string groupId)
    {
        try
        {
            var serviceClient = new TableServiceClient(env["AzureWebJobsStorage"]);
            var exportTableOptions = Options.Create(new ExportTableOptions { TableName = env["ExportTable__TableName"] });
            var exporter = new ExportTableExporter(serviceClient, exportTableOptions);
            await exporter.DeleteGroupAsync(groupId, CancellationToken.None);
        }
        catch
        {
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var sln = Path.Combine(dir.FullName, "wedding.sln");
            if (File.Exists(sln))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root (wedding.sln not found). Run tests from within the repo.");
    }

}
