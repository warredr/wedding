using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using WeddingApi.Domain;

namespace WeddingApi.Invites;

public sealed class InvitesOptions
{
    public const string SectionName = "Invites";

    public string? JsonPath { get; init; }

    public int CacheSeconds { get; init; } = 300;
}

public sealed class JsonInviteRepository : IInviteRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly InvitesOptions _options;
    private readonly object _gate = new();

    private DateTimeOffset _cacheUntilUtc = DateTimeOffset.MinValue;
    private InvitesFile? _cached;
    private IReadOnlyDictionary<string, GroupDefinition>? _groupsById;
    private IReadOnlyList<PersonSearchHit>? _people;

    public JsonInviteRepository(IOptions<InvitesOptions> options)
    {
        _options = options.Value;
    }

    public Task<IReadOnlyList<PersonSearchHit>> SearchPeopleAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<PersonSearchHit>>(Array.Empty<PersonSearchHit>());
        }

        EnsureLoaded();

        var normalized = query.Trim();
        var hits = _people!
            .Where(p => p.FullName.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.FullName)
            .Take(25)
            .ToList();

        return Task.FromResult<IReadOnlyList<PersonSearchHit>>(new ReadOnlyCollection<PersonSearchHit>(hits));
    }

    public Task<GroupDefinition?> GetGroupAsync(string groupId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return Task.FromResult<GroupDefinition?>(null);
        }

        EnsureLoaded();
        _groupsById!.TryGetValue(groupId, out var group);
        return Task.FromResult<GroupDefinition?>(group);
    }

    public Task<IReadOnlyList<GroupDefinition>> GetAllGroupsAsync(CancellationToken cancellationToken)
    {
        EnsureLoaded();
        return Task.FromResult<IReadOnlyList<GroupDefinition>>(_groupsById!.Values.ToList());
    }

    private void EnsureLoaded()
    {
        var now = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            if (_cached is not null && now < _cacheUntilUtc)
            {
                return;
            }

            var path = ResolvePath(_options.JsonPath);
            if (path is null)
            {
                throw new InvalidOperationException("Invites JSON path not configured.");
            }

            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<InvitesFile>(json, SerializerOptions);
            if (parsed is null)
            {
                throw new InvalidOperationException("Unable to parse invites JSON.");
            }

            var groupsById = new Dictionary<string, GroupDefinition>(StringComparer.Ordinal);
            var people = new List<PersonSearchHit>();

            foreach (var g in parsed.Groups)
            {
                var members = g.Members
                    .Select(m => new PersonDefinition(m.PersonId, m.FullName))
                    .ToList()
                    .AsReadOnly();

                var group = new GroupDefinition(
                    GroupId: g.GroupId,
                    GroupLabelFirstNames: g.GroupLabelFirstNames,
                    InvitedTo: g.InvitedTo.AsReadOnly(),
                    Members: members);

                groupsById[g.GroupId] = group;

                foreach (var member in members)
                {
                    people.Add(new PersonSearchHit(
                        PersonId: member.PersonId,
                        FullName: member.FullName,
                        GroupId: group.GroupId,
                        GroupLabelFirstNames: group.GroupLabelFirstNames));
                }
            }

            _cached = parsed;
            _groupsById = new ReadOnlyDictionary<string, GroupDefinition>(groupsById);
            _people = new ReadOnlyCollection<PersonSearchHit>(people);
            _cacheUntilUtc = now.AddSeconds(Math.Max(5, _options.CacheSeconds));
        }
    }

    private static string? ResolvePath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        // Default: packaged content file under the Functions app directory
        var baseDir = AppContext.BaseDirectory;
        var defaultPath = Path.Combine(baseDir, "Data", "invites.json");
        return File.Exists(defaultPath) ? defaultPath : null;
    }
}
