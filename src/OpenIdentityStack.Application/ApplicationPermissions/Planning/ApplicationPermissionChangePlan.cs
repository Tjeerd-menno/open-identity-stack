using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.ApplicationPermissions.Planning;

public sealed record ApplicationPermissionChangePlan(
    RegisteredApplication Application,
    IReadOnlyList<ApplicationPermission> Additions,
    IReadOnlyList<ApplicationPermission> MetadataUpdates,
    IReadOnlyList<ApplicationPermission> Removals,
    PermissionAssignmentRemovalPlan AssignmentRemovalPlan,
    PermissionAssignmentRemovalPlan WildcardExpansionImpactPlan)
{
    public bool HasChanges => this.Additions.Count > 0 || this.MetadataUpdates.Count > 0 || this.Removals.Count > 0;

    public bool IsDestructive => this.Removals.Count > 0;
}
