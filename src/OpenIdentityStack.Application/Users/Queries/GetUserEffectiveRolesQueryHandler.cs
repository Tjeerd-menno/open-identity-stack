using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Queries;

/// <summary>
/// Implementation of the handler for getting effective roles.
/// </summary>
public sealed class GetUserEffectiveRolesQueryHandler : IGetUserEffectiveRolesQueryHandler
{
    private readonly IRoleRepository roleRepository;
    private readonly IGroupRepository groupRepository;

    public GetUserEffectiveRolesQueryHandler(
        IRoleRepository roleRepository,
        IGroupRepository groupRepository)
    {
        this.roleRepository = roleRepository;
        this.groupRepository = groupRepository;
    }

    public async Task<Result<IReadOnlyList<RoleDto>>> HandleAsync(
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        // 1. Get Direct Roles
        IReadOnlyList<Role> directRoles = await this.roleRepository.GetUserRolesAsync(userId, activeOnly: true, cancellationToken);
        var effectiveRolesMap = directRoles.ToDictionary(r => r.Id, r => r);

        // 2. Get Groups
        IReadOnlyList<Group> groups = await this.groupRepository.GetGroupsForUserAsync(userId, cancellationToken);

        // 3. Process Group Mappings
        var roleNamesToResolve = new HashSet<string>();
        foreach (Group group in groups)
        {
            foreach (GroupMapping mapping in group.Mappings)
            {
                if (mapping.Type == MappingType.Role)
                {
                    // Target is RoleName
                    // Avoid duplicates in resolution
                    roleNamesToResolve.Add(mapping.Target);
                }
            }
        }

        // 4. Resolve indirect roles
        foreach (string roleName in roleNamesToResolve)
        {
            // Optimization: If we already have a direct role with this Name, skip?
            // But Map is keyed by Id. RoleName <-> RoleId link not in map unless we scan values.
            if (effectiveRolesMap.Values.Any(r => r.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            Role? role = await this.roleRepository.GetByNameAsync(roleName, cancellationToken);
            if (role is null && Guid.TryParse(roleName, out Guid roleId))
            {
                role = await this.roleRepository.GetByIdAsync(new RoleId(roleId), cancellationToken);
            }
            if (role != null && role.IsActive)
            {
                effectiveRolesMap.TryAdd(role.Id, role);
            }
        }

        // 5. Convert to DTOs
        var dtos = effectiveRolesMap.Values.Select(role => new RoleDto(
            role.Id.Value,
            role.Name,
            role.DisplayName,
            role.Description,
            role.IsSystemRole,
            role.IsActive,
            role.Permissions
        )).ToList();

        return dtos;
    }
}
