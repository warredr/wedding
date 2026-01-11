using Microsoft.Extensions.Options;
using WeddingApi.Domain;
using WeddingApi.Invites;

namespace wedding_api.Tests;

public sealed class JsonInviteRepositoryTests
{
    [Fact]
    public async Task SearchPeopleAsync_FindsMatches_CaseInsensitive_AndIncludesGroupInfo()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wedding-api-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var jsonPath = Path.Combine(tempDir, "invites.json");

        await File.WriteAllTextAsync(jsonPath,
            """
            {
              "schemaVersion": 1,
              "groups": [
                {
                  "groupId": "g1",
                  "groupLabelFirstNames": "Alice & Bob",
                  "invitedTo": ["Dinner"],
                  "members": [
                    { "personId": "p1", "fullName": "Alice Example" },
                    { "personId": "p2", "fullName": "Bob Example" }
                  ]
                }
              ]
            }
            """);

        try
        {
            var repo = new JsonInviteRepository(Options.Create(new InvitesOptions
            {
                JsonPath = jsonPath,
                CacheSeconds = 60,
            }));

            var hits = await repo.SearchPeopleAsync("ali", CancellationToken.None);

            Assert.Single(hits);
            Assert.Equal("p1", hits[0].PersonId);
            Assert.Equal("Alice Example", hits[0].FullName);
            Assert.Equal("g1", hits[0].GroupId);
            Assert.Equal("Alice & Bob", hits[0].GroupLabelFirstNames);

            var group = await repo.GetGroupAsync("g1", CancellationToken.None);
            Assert.NotNull(group);
            Assert.Equal("g1", group!.GroupId);
            Assert.Equal("Alice & Bob", group.GroupLabelFirstNames);
            Assert.Contains(EventType.Dinner, group.InvitedTo);
            Assert.Equal(2, group.Members.Count);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
