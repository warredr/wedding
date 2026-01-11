using System.Text.Json.Serialization;
using WeddingApi.Domain;

namespace WeddingApi.Invites;

public sealed class InvitesFile
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("groups")]
    public List<InviteGroup> Groups { get; init; } = new();
}

public sealed class InviteGroup
{
    [JsonPropertyName("groupId")]
    public required string GroupId { get; init; }

    [JsonPropertyName("groupLabelFirstNames")]
    public required string GroupLabelFirstNames { get; init; }

    [JsonPropertyName("invitedTo")]
    public List<EventType> InvitedTo { get; init; } = new();

    [JsonPropertyName("members")]
    public List<InvitePerson> Members { get; init; } = new();
}

public sealed class InvitePerson
{
    [JsonPropertyName("personId")]
    public required string PersonId { get; init; }

    [JsonPropertyName("fullName")]
    public required string FullName { get; init; }
}
