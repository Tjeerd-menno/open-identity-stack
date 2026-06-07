using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Application.ApplicationPermissions.Planning;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Infrastructure.Persistence;

namespace OpenIdentityStack.Infrastructure.ApplicationPermissions;

public sealed class RolePermissionAssignmentStore : IPermissionAssignmentStore
{
    private const string assignmentStore = "role";

    private readonly OpenIdentityStackDbContext dbContext;

    public RolePermissionAssignmentStore(OpenIdentityStackDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PermissionAssignmentImpactDto>> PreviewRemovalImpactAsync(
        PermissionAssignmentRemovalPlan plan,
        CancellationToken cancellationToken = default)
    {
        List<Role> roles = await this.dbContext.Roles
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return BuildImpacts(roles, plan);
    }

    public async Task<Result<IReadOnlyList<PermissionAssignmentImpactDto>>> RemoveAssignmentsAsync(
        PermissionAssignmentRemovalPlan plan,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        _ = actorId;

        List<Role> roles = await this.dbContext.Roles
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<PermissionAssignmentImpactDto> impacts = BuildImpacts(roles, plan);
        var permissionsToRemove = BuildPermissionSet(plan.ExactPermissions)
            .Concat(BuildPermissionSet(plan.WildcardPermissionsToRemove))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (Role role in roles)
        {
            var retainedPermissions = role.Permissions
                .Where(permission => !permissionsToRemove.Contains(permission))
                .ToList();

            if (retainedPermissions.Count != role.Permissions.Count)
            {
                role.SetPermissions(retainedPermissions);
            }
        }

        await this.dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return impacts;
    }

    private static List<PermissionAssignmentImpactDto> BuildImpacts(
        IReadOnlyList<Role> roles,
        PermissionAssignmentRemovalPlan plan)
    {
        HashSet<string> exactPermissions = BuildPermissionSet(plan.ExactPermissions);
        HashSet<string> wildcardPermissionsToRemove = BuildPermissionSet(plan.WildcardPermissionsToRemove);
        var impactedWildcardPermissions = exactPermissions
            .Select(ToResourceWildcard)
            .Where(static wildcard => wildcard is not null)
            .Cast<string>()
            .Where(wildcard => !wildcardPermissionsToRemove.Contains(wildcard))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var impacts = new List<PermissionAssignmentImpactDto>();
        foreach (Role role in roles)
        {
            foreach (string permission in role.Permissions)
            {
                if (exactPermissions.Contains(permission))
                {
                    impacts.Add(ToImpact(role, permission, AssignmentImpactKinds.ExactRemoved));
                }
                else if (wildcardPermissionsToRemove.Contains(permission))
                {
                    impacts.Add(ToImpact(role, permission, AssignmentImpactKinds.WildcardRemoved));
                }
                else if (impactedWildcardPermissions.Contains(permission))
                {
                    impacts.Add(ToImpact(role, permission, AssignmentImpactKinds.WildcardImpacted));
                }
            }
        }

        return impacts;
    }

    private static PermissionAssignmentImpactDto ToImpact(Role role, string permission, string impactKind)
    {
        return new PermissionAssignmentImpactDto(
            assignmentStore,
            role.Id.Value.ToString(),
            role.DisplayName,
            permission,
            impactKind);
    }

    private static HashSet<string> BuildPermissionSet(IEnumerable<string> permissions)
    {
        return permissions
            .Where(static permission => !string.IsNullOrWhiteSpace(permission))
            .Select(static permission => permission.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string? ToResourceWildcard(string fullPermissionKey)
    {
        string[] parts = fullPermissionKey.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 3 ? $"{parts[0]}:{parts[1]}:*" : null;
    }
}
