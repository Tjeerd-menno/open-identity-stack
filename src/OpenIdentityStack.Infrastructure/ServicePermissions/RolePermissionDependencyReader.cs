using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.ServicePermissions;
using OpenIdentityStack.Infrastructure.Persistence;

namespace OpenIdentityStack.Infrastructure.ServicePermissions;

public sealed class RolePermissionDependencyReader : IRolePermissionDependencyReader
{
    private readonly OpenIdentityStackDbContext dbContext;

    public RolePermissionDependencyReader(OpenIdentityStackDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<IReadOnlyList<RoleAssignmentDependency>> GetDependenciesAsync(
        string fullPermissionKey,
        CancellationToken cancellationToken = default)
    {
        string normalized = fullPermissionKey.Trim().ToLowerInvariant();
        string serviceWildcard = normalized.Contains(':', StringComparison.Ordinal)
            ? normalized[..normalized.IndexOf(':', StringComparison.Ordinal)] + ":*"
            : normalized;

        List<Role> roles = await this.dbContext.Roles
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return roles
            .Where(role => role.Permissions.Any(permission =>
                string.Equals(permission, normalized, StringComparison.OrdinalIgnoreCase)
                || string.Equals(permission, serviceWildcard, StringComparison.OrdinalIgnoreCase)
                || string.Equals(permission, "*", StringComparison.OrdinalIgnoreCase)))
            .Select(role => new RoleAssignmentDependency(
                normalized,
                DependencyType.Role,
                role.Id.Value.ToString(),
                role.DisplayName,
                role.IsActive,
                role.IsActive ? DependencyImpact.BlocksRetirement : DependencyImpact.WarningOnly))
            .ToList();
    }
}
