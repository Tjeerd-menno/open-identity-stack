using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Domain.Resources;
using SharedKernel;

namespace OpenIdentityStack.Application.AdministrativeAccess;

public sealed class AdministrativeAccessEvaluator(IResourcePermissionService resources, IAdministrativeAuthoritySnapshot authoritySnapshot) : IAdministrativeAccessEvaluator
{
    public async Task<Result<IReadOnlyList<string>>> EvaluateAsync(AdministrativeAccessRequest request, CancellationToken cancellationToken = default)
    {
        // Bind the later mutation fence to these authorization reads. A use-case capture
        // must reuse this revision rather than accepting a revocation that happened in between.
        await authoritySnapshot.CaptureAsync(cancellationToken);
        Result<ResourceTokenProjection> current = await resources.ProjectAsync(new ResourceTokenRequest(request.ClientId,
            [ProtectedResource.AdministrativeScope], [ProtectedResource.AdministrativeAudience], request.UserId,
            request.TokenPermissions, [ProtectedResource.AdministrativeAudience]), cancellationToken);
        if (current.IsFailure) { return current.Error; }
        if (!current.Value.Audiences.SequenceEqual([ProtectedResource.AdministrativeAudience]) || current.Value.Permissions.Count == 0)
        {
            return ResourceAccessErrors.NotGranted;
        }
        return current.Value.Permissions.ToList();
    }
}
