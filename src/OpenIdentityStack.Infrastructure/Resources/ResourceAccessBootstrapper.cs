using OpenIddict.Abstractions;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Domain.Resources;

namespace OpenIdentityStack.Infrastructure.Resources;

/// <summary>Initializes the reserved resource only. Client entitlements require explicit approval.</summary>
public sealed class ResourceAccessBootstrapper(IResourceAccessRepository resources, IOpenIddictScopeManager scopes)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ProtectedResource? resource = await resources.GetResourceAsync(ProtectedResource.AdministrativeResourceId, cancellationToken);
        bool created = resource is null;
        if (resource is null)
        {
            resource = ProtectedResource.CreateAdministrative();
            resources.AddResource(resource);
        }
        object? existing = await scopes.FindByNameAsync(resource.Scope, cancellationToken);
        if (created || existing is null || !(await scopes.GetResourcesAsync(existing, cancellationToken)).SequenceEqual([resource.Audience]))
        {
            await resources.SaveChangesAsync("system", "AdministrativeResourceInitialized", resource.Id.ToString(), resource, cancellationToken);
        }
    }
}
