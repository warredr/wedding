using WeddingApi.Domain;

namespace WeddingApi.Invites;

public sealed record PersonSearchHit(
    string PersonId,
    string FullName,
    string GroupId,
    string GroupLabelFirstNames
);

public interface IInviteRepository
{
    Task<IReadOnlyList<PersonSearchHit>> SearchPeopleAsync(string query, CancellationToken cancellationToken);
    Task<GroupDefinition?> GetGroupAsync(string groupId, CancellationToken cancellationToken);
    Task<IReadOnlyList<GroupDefinition>> GetAllGroupsAsync(CancellationToken cancellationToken);
}
