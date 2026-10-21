using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Queries;

public class ListGroupMappingsQueryHandler : IListGroupMappingsQueryHandler
{
    private readonly IGroupRepository repository;
    private readonly IRoleRepository roles;

    public ListGroupMappingsQueryHandler(IGroupRepository repository, IRoleRepository roles)
    {
        this.repository = repository;
        this.roles = roles;
    }

    public async Task<Result<List<GroupMappingResponse>>> HandleAsync(
        GroupId groupId,
        CancellationToken cancellationToken = default)
    {
        Group? group = await this.repository.GetByIdAsync(groupId, cancellationToken);
        if (group is null)
        {
            return new DomainError("Group.NotFound", "Group not found");
        }

        var roleIds = new List<RoleId>();
        foreach (GroupMapping mapping in group.Mappings)
        {
            if (mapping.Type == MappingType.Role && Guid.TryParse(mapping.Target, out Guid roleId))
            {
                roleIds.Add(new RoleId(roleId));
            }
        }

        Dictionary<Guid, string> roleNames = [];
        if (roleIds.Count > 0)
        {
            IReadOnlyList<Role> found = await this.roles.GetByIdsAsync(roleIds, cancellationToken);
            foreach (Role role in found)
            {
                roleNames[role.Id.Value] = role.Name;
            }
        }

        var response = group.Mappings.Select(mapping =>
        {
            Guid? targetRoleId = mapping.Type == MappingType.Role && Guid.TryParse(mapping.Target, out Guid parsed)
                ? parsed
                : null;
            string? roleName = targetRoleId is Guid id && roleNames.TryGetValue(id, out string? name)
                ? name
                : null;
            return new GroupMappingResponse(
                mapping.GetDeterministicId(),
                mapping.Type,
                targetRoleId,
                mapping.Type == MappingType.Claim ? mapping.Target : null,
                mapping.Value,
                mapping.TokenTarget,
                roleName);
        }).ToList();

        return response;
    }
}
