using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Authorization;

/// <summary>
/// Implementation of permission checking based on user roles.
/// </summary>
public sealed class PermissionChecker : IPermissionChecker
{
    private readonly IRoleRepository roleRepository;
    private PermissionSnapshot? snapshot;

    public PermissionChecker(IRoleRepository roleRepository)
    {
        this.roleRepository = roleRepository;
    }

    public async Task<bool> HasPermissionAsync(
        UserId userId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> permissions = await this.GetAllPermissionsAsync(userId, cancellationToken);
        return permissions.Any(p => Permissions.Matches(p, permission));
    }

    public async Task<bool> HasAnyPermissionAsync(
        UserId userId,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> userPermissions = await this.GetAllPermissionsAsync(userId, cancellationToken);
        return permissions.Any(required => 
            userPermissions.Any(granted => Permissions.Matches(granted, required)));
    }

    public async Task<bool> HasAllPermissionsAsync(
        UserId userId,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> userPermissions = await this.GetAllPermissionsAsync(userId, cancellationToken);
        return permissions.All(required => 
            userPermissions.Any(granted => Permissions.Matches(granted, required)));
    }

    public async Task<IReadOnlyList<string>> GetAllPermissionsAsync(
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        // The checker is scoped per request and a user's role assignments cannot change within that
        // scope, so resolve the role graph once instead of on every check.
        if (this.snapshot is { } cached && cached.UserId == userId)
        {
            return cached.Permissions;
        }

        // Get roles from the RoleAssignments table via the repository
        IReadOnlyList<Role> roles = await this.roleRepository.GetUserRolesAsync(userId, activeOnly: true, cancellationToken);
        
        var allPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Role role in roles)
        {
            foreach (string permission in role.Permissions)
            {
                allPermissions.Add(permission);
            }
        }

        string[] resolved = [.. allPermissions];
        this.snapshot = new PermissionSnapshot(userId, resolved);

        return resolved;
    }

    private sealed record PermissionSnapshot(UserId UserId, string[] Permissions);
}
