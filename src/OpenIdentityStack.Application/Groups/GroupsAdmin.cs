using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Groups.Commands;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Application.Groups;

internal sealed class GroupsAdmin : IGroupsAdmin
{
    private readonly IListGroupsQueryHandler listGroups;
    private readonly ICreateGroupUseCase createGroup;
    private readonly IGetGroupQueryHandler getGroup;
    private readonly IUpdateGroupUseCase updateGroup;
    private readonly IDeleteGroupUseCase deleteGroup;
    private readonly IListGroupMembersQueryHandler listMembers;
    private readonly IAddUserToGroupUseCase addMember;
    private readonly IRemoveUserFromGroupUseCase removeMember;
    private readonly IListGroupMappingsQueryHandler listMappings;
    private readonly IAddGroupMappingUseCase addMapping;
    private readonly IRemoveGroupMappingUseCase removeMapping;
    private readonly IRoleRepository roles;

    internal GroupsAdmin(
        IListGroupsQueryHandler listGroups,
        ICreateGroupUseCase createGroup,
        IGetGroupQueryHandler getGroup,
        IUpdateGroupUseCase updateGroup,
        IDeleteGroupUseCase deleteGroup,
        IListGroupMembersQueryHandler listMembers,
        IAddUserToGroupUseCase addMember,
        IRemoveUserFromGroupUseCase removeMember,
        IListGroupMappingsQueryHandler listMappings,
        IAddGroupMappingUseCase addMapping,
        IRemoveGroupMappingUseCase removeMapping,
        IRoleRepository roles)
    {
        this.listGroups = listGroups;
        this.createGroup = createGroup;
        this.getGroup = getGroup;
        this.updateGroup = updateGroup;
        this.deleteGroup = deleteGroup;
        this.listMembers = listMembers;
        this.addMember = addMember;
        this.removeMember = removeMember;
        this.listMappings = listMappings;
        this.addMapping = addMapping;
        this.removeMapping = removeMapping;
        this.roles = roles;
    }

    public Task<Result<GroupListResponse>> ListAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        CancellationToken cancellationToken = default) =>
        this.listGroups.HandleAsync(page, pageSize, search, cancellationToken);

    public Task<Result<CreateGroupResponse>> CreateAsync(CreateGroupCommand command, CancellationToken cancellationToken = default) =>
        this.createGroup.ExecuteAsync(command, cancellationToken);

    public Task<Group?> GetAsync(GroupId id, CancellationToken cancellationToken = default) =>
        this.getGroup.HandleAsync(id, cancellationToken);

    public Task<Result> UpdateAsync(UpdateGroupCommand command, CancellationToken cancellationToken = default) =>
        this.updateGroup.ExecuteAsync(command, cancellationToken);

    public Task<Result> DeleteAsync(DeleteGroupCommand command, CancellationToken cancellationToken = default) =>
        this.deleteGroup.ExecuteAsync(command, cancellationToken);

    public Task<Result<UserListResponse>> ListMembersAsync(
        GroupId groupId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        this.listMembers.HandleAsync(groupId, page, pageSize, cancellationToken);

    public Task<Result> AddMemberAsync(AddUserToGroupCommand command, CancellationToken cancellationToken = default) =>
        this.addMember.ExecuteAsync(command, cancellationToken);

    public Task<Result> RemoveMemberAsync(RemoveUserFromGroupCommand command, CancellationToken cancellationToken = default) =>
        this.removeMember.ExecuteAsync(command, cancellationToken);

    public async Task<Result<IReadOnlyList<ListedGroupMapping>>> ListMappingsAsync(
        GroupId groupId,
        CancellationToken cancellationToken = default)
    {
        Result<List<GroupMappingResponse>> result = await this.listMappings.HandleAsync(groupId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        var roleIds = result.Value
            .Where(mapping => mapping.MappingType == MappingType.Role && mapping.TargetRoleId.HasValue)
            .Select(mapping => new RoleId(mapping.TargetRoleId!.Value))
            .Distinct()
            .ToList();

        Dictionary<Guid, string> roleNames = [];
        if (roleIds.Count > 0)
        {
            IReadOnlyList<Role> found = await this.roles.GetByIdsAsync(roleIds, cancellationToken);
            foreach (Role role in found)
            {
                roleNames[role.Id.Value] = role.Name;
            }
        }

        var items = result.Value.Select(mapping =>
        {
            string value;
            if (mapping.MappingType == MappingType.Role && mapping.TargetRoleId.HasValue)
            {
                value = roleNames.TryGetValue(mapping.TargetRoleId.Value, out string? roleName)
                    ? roleName
                    : mapping.TargetRoleId.Value.ToString();
            }
            else
            {
                value = string.IsNullOrEmpty(mapping.ClaimValue)
                    ? mapping.ClaimType ?? string.Empty
                    : $"{mapping.ClaimType}:{mapping.ClaimValue}";
            }

            return new ListedGroupMapping(mapping.Id, mapping.MappingType, value);
        }).ToList();

        return items;
    }

    public Task<Result> AddMappingAsync(AddGroupMappingCommand command, CancellationToken cancellationToken = default) =>
        this.addMapping.ExecuteAsync(command, cancellationToken);

    public Task<Result> RemoveMappingAsync(RemoveGroupMappingCommand command, CancellationToken cancellationToken = default) =>
        this.removeMapping.ExecuteAsync(command, cancellationToken);
}
