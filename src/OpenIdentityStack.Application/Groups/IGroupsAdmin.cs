using OpenIdentityStack.Application.Groups.Commands;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Application.Groups;

/// <summary>
/// Group administration the API may call. Group use cases stay behind this type.
/// </summary>
public interface IGroupsAdmin
{
    Task<Result<GroupListResponse>> ListAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<Result<CreateGroupResponse>> CreateAsync(CreateGroupCommand command, CancellationToken cancellationToken = default);

    Task<Group?> GetAsync(GroupId id, CancellationToken cancellationToken = default);

    Task<Result> UpdateAsync(UpdateGroupCommand command, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(DeleteGroupCommand command, CancellationToken cancellationToken = default);

    Task<Result<UserListResponse>> ListMembersAsync(
        GroupId groupId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<Result> AddMemberAsync(AddUserToGroupCommand command, CancellationToken cancellationToken = default);

    Task<Result> RemoveMemberAsync(RemoveUserFromGroupCommand command, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ListedGroupMapping>>> ListMappingsAsync(GroupId groupId, CancellationToken cancellationToken = default);

    Task<Result> AddMappingAsync(AddGroupMappingCommand command, CancellationToken cancellationToken = default);

    Task<Result> RemoveMappingAsync(RemoveGroupMappingCommand command, CancellationToken cancellationToken = default);
}
