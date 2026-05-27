using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Roles;

namespace OpenIdentityStack.Infrastructure.Persistence.Applications;

public sealed class ApplicationPermissionMigration
{
    private static readonly Dictionary<string, string> permissionMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["clients:read"] = "applications:read",
        ["clients:write"] = "applications:write",
        ["clients:delete"] = "applications:delete",
        ["clients:manage-secrets"] = "applications:manage-credentials",
        ["clients:*"] = "applications:*",
        ["service-accounts:read"] = "applications:read",
        ["service-accounts:write"] = "applications:write",
        ["service-accounts:delete"] = "applications:delete",
        ["service-accounts:rotate-secret"] = "applications:manage-credentials",
        ["service-accounts:manage-certificates"] = "applications:manage-certificates",
        ["service-accounts:*"] = "applications:*"
    };

    private readonly OpenIdentityStackDbContext dbContext;

    public ApplicationPermissionMigration(OpenIdentityStackDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<ApplicationPermissionMigrationResult> MigrateAsync(CancellationToken cancellationToken = default)
    {
        List<Role> roles = await this.dbContext.Roles.ToListAsync(cancellationToken);
        int updatedRoles = 0;

        foreach (Role role in roles)
        {
            List<string> migratedPermissions = MapPermissions(role.Permissions);
            if (role.Permissions.SequenceEqual(migratedPermissions, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            role.SetPermissions(migratedPermissions);
            updatedRoles++;
        }

        await this.dbContext.SaveChangesAsync(cancellationToken);
        return new ApplicationPermissionMigrationResult(updatedRoles);
    }

    private static List<string> MapPermissions(IReadOnlyList<string> permissions)
    {
        var migratedPermissions = new List<string>();
        var seenPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string permission in permissions)
        {
            string mappedPermission = permissionMappings.TryGetValue(permission, out string? mapped)
                ? mapped
                : permission;
            if (seenPermissions.Add(mappedPermission))
            {
                migratedPermissions.Add(mappedPermission);
            }
        }

        return migratedPermissions;
    }
}

public sealed record ApplicationPermissionMigrationResult(int UpdatedRoles);
