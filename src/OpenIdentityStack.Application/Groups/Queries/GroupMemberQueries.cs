using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Queries;

// For simplicity, reusing a standard UserListResponse if available, or creating DTO
public record UserListResponse(IEnumerable<UserListItem> Items, string? NextPageToken);

public class ListGroupMembersQueryHandler : IListGroupMembersQueryHandler
{
    private readonly IGroupRepository groupRepository;
    private readonly IUserRepository userRepository; // Need to fetch user details

    public ListGroupMembersQueryHandler(IGroupRepository groupRepository, IUserRepository userRepository)
    {
        this.groupRepository = groupRepository;
        this.userRepository = userRepository;
    }

    public async Task<Result<UserListResponse>> HandleAsync(
        GroupId groupId,
        int page = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            Group? group = await this.groupRepository.GetByIdAsync(groupId, cancellationToken);
            if (group is null)
        {
            return new DomainError("Group.NotFound", "Group not found");
        }

        // Paginate membership IDs first (preserving AssignedAt-desc order), then batch-load
        // the corresponding users. Missing users are skipped without backfilling the page.
        var memberIds = group.Memberships
            .OrderByDescending(m => m.AssignedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => m.UserId)
            .ToList();

        IReadOnlyList<User> loadedUsers = await this.userRepository.GetByIdsAsync(memberIds, cancellationToken);
        var usersById = loadedUsers.ToDictionary(u => u.Id);

        var users = new List<UserListItem>(memberIds.Count);
        foreach (UserId userId in memberIds)
        {
            if (usersById.TryGetValue(userId, out User? user))
            {
                users.Add(new UserListItem(user.Id, user.Email!, user.DisplayName ?? string.Empty, user.Status, user.CreatedAt));
            }
        }

        return new UserListResponse(users, null);
    }
}
