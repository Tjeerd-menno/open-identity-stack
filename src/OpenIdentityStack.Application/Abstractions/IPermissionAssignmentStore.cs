using OpenIdentityStack.Application.ApplicationPermissions.Dtos;

namespace OpenIdentityStack.Application.Abstractions;

public interface IPermissionAssignmentStore
{
    Task<IReadOnlyList<PermissionAssignmentImpactDto>> PreviewRemovalImpactAsync(
        PermissionAssignmentRemovalPlan plan,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PermissionAssignmentImpactDto>>> RemoveAssignmentsAsync(
        PermissionAssignmentRemovalPlan plan,
        string actorId,
        CancellationToken cancellationToken = default);
}
