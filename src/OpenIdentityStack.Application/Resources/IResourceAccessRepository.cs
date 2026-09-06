using ApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;
using OpenIdentityStack.Domain.Resources;

namespace OpenIdentityStack.Application.Resources;

public interface IResourceAccessRepository
{
    Task<IReadOnlyList<ProtectedResource>> ListResourcesAsync(CancellationToken cancellationToken = default);
    Task<ProtectedResource?> GetResourceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProtectedResource?> FindByScopeAsync(string scope, CancellationToken cancellationToken = default);
    Task<ProtectedResource?> FindByAudienceAsync(string audience, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientResourceGrant>> ListGrantsAsync(ApplicationId applicationId, CancellationToken cancellationToken = default);
    Task<ClientResourceGrant?> GetGrantAsync(ApplicationId applicationId, Guid resourceId, CancellationToken cancellationToken = default);
    void AddResource(ProtectedResource resource);
    void AddGrant(ClientResourceGrant grant);
    void RemoveGrant(ClientResourceGrant grant);
    Task SaveChangesAsync(string actorId, string action, string entityId, ProtectedResource? projectResource = null, CancellationToken cancellationToken = default);
}
