using SharedKernel;

namespace OpenIdentityStack.Application.Resources;

public sealed record ResourceTokenRequest(
    string ClientId,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> RequestedResources,
    UserId? UserId,
    IReadOnlyList<string>? OriginalPermissions = null,
    IReadOnlyList<string>? OriginalAudiences = null);

public sealed record ResourceTokenProjection(
    IReadOnlyList<string> Audiences,
    IReadOnlyList<string> Permissions,
    IReadOnlyDictionary<Guid, long> GrantRevisions);

public interface IResourcePermissionService
{
    Task<Result<ResourceTokenProjection>> ProjectAsync(ResourceTokenRequest request, CancellationToken cancellationToken = default);
}
