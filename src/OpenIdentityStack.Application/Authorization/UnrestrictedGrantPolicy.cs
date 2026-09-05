using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Groups;
using SharedKernel;

namespace OpenIdentityStack.Application.Authorization;

public sealed class UnrestrictedGrantPolicy(IRoleRepository roles)
{
    public static bool IncludesAllPermissions(IEnumerable<string> permissions) =>
        permissions.Any(permission => string.Equals(permission.Trim(), "*", StringComparison.Ordinal));

    public async Task<bool> RoleIsUnrestrictedAsync(string roleName, CancellationToken cancellationToken)
    {
        OpenIdentityStack.Domain.Roles.Role? role = Guid.TryParse(roleName, out Guid roleId)
            ? await roles.GetByIdAsync(new RoleId(roleId), cancellationToken)
            : await roles.GetByNameAsync(roleName, cancellationToken);
        return role is not null && IncludesAllPermissions(role.Permissions);
    }

    public async Task<bool> GroupIsUnrestrictedAsync(Group group, CancellationToken cancellationToken)
    {
        foreach (GroupMapping mapping in group.Mappings.Where(mapping => mapping.Type == MappingType.Role))
        {
            if (await this.RoleIsUnrestrictedAsync(mapping.Target, cancellationToken))
            {
                return true;
            }
        }
        return false;
    }
}
