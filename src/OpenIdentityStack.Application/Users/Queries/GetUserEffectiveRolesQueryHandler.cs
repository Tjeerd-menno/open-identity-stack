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
        var directRoleNames = new HashSet<string>(directRoles.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);

        // 2. Get Groups
        IReadOnlyList<Group> groups = await this.groupRepository.GetGroupsForUserAsync(userId, cancellationToken);

        // 3. Collect group-mapped role names not already granted directly
        var roleNamesToResolve = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Group group in groups)
        {
            foreach (GroupMapping mapping in group.Mappings)
            {
                if (mapping.Type != MappingType.Role)
                {
                    continue;
                }

                string target = mapping.Target.Trim();
                if (target.Length > 0 && !directRoleNames.Contains(target))
                {
                    roleNamesToResolve.Add(target);
                }
            }
        }

        // 4. Resolve indirect roles in batched queries. A role whose name matches the mapping target
        //    always takes precedence; only unmatched GUID-shaped targets fall back to lookup by id.
        if (roleNamesToResolve.Count > 0)
        {
            IReadOnlyList<Role> rolesByName = await this.roleRepository.GetByNamesAsync(roleNamesToResolve, cancellationToken);
            var resolvedNames = new HashSet<string>(rolesByName.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);
            AddActiveRoles(effectiveRolesMap, rolesByName);

            var fallbackIds = new List<RoleId>();
            foreach (string roleName in roleNamesToResolve)
            {
                if (!resolvedNames.Contains(roleName) && Guid.TryParse(roleName, out Guid roleId))
                {
                    fallbackIds.Add(new RoleId(roleId));
                }
            }

            if (fallbackIds.Count > 0)
            {
                IReadOnlyList<Role> rolesById = await this.roleRepository.GetByIdsAsync(fallbackIds, cancellationToken);
                AddActiveRoles(effectiveRolesMap, rolesById);
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

    private static void AddActiveRoles(Dictionary<RoleId, Role> effectiveRolesMap, IReadOnlyList<Role> roles)
    {
        foreach (Role role in roles)
        {
            if (role.IsActive)
            {
                effectiveRolesMap.TryAdd(role.Id, role);
            }
        }
    }
}
