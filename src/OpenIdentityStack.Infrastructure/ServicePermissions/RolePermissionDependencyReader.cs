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

        // Use PostgreSQL JSONB containment operator to filter in the database
        // The @> operator checks if the left JSONB value contains the right JSONB value
        List<RoleAssignmentDependency> dependencies = await this.dbContext.Roles
            .AsNoTracking()
            .Where(role =>
                EF.Functions.JsonContains(role.Permissions, $"[\"{normalized}\"]") ||
                EF.Functions.JsonContains(role.Permissions, $"[\"{serviceWildcard}\"]") ||
                EF.Functions.JsonContains(role.Permissions, "[\"*\"]"))
            .Select(role => new RoleAssignmentDependency(
                normalized,
                DependencyType.Role,
                role.Id.Value.ToString(),
                role.DisplayName,
                role.IsActive,
                role.IsActive ? DependencyImpact.BlocksRetirement : DependencyImpact.WarningOnly))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return dependencies;
    }
}
