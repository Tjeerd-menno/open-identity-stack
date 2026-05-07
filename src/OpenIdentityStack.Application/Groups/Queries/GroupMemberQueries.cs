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

        // Simple in-memory pagination for MVP. Ideally repo supports specific query.
        var memberIds = group.Memberships
            .OrderByDescending(m => m.AssignedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => m.UserId)
            .ToList();

        // N+1 or batch load. IUserRepository might need GetBatchAsync.
        // Assuming getting individualy for now or iterating.
        // Actually, let's just return IDs if User details aren't strictly required by spec?
        // Spec says "List group members" -> "UserListResponse" -> "UserResponse".
        // I need to fetch users. I'll rely on generic ListAsync with filter?
        // Or adding `GetUsersByIdsAsync` to IUserRepository.
        
        var users = new List<UserListItem>();
        foreach(UserId userId in memberIds)
        {
            User? user = await this.userRepository.GetByIdAsync(userId, cancellationToken);
             if (user is not null)
             {
                 users.Add(new UserListItem(user.Id, user.Email!, user.DisplayName ?? string.Empty, user.Status, user.CreatedAt));
             }
        }

        return new UserListResponse(users, null);
    }
}
