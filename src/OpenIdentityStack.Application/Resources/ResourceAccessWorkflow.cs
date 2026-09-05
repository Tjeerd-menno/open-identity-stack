using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.Resources;
using SharedKernel;
using ApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Resources;

public sealed record ProtectedResourceDto(Guid Id, string Audience, string Scope, string DisplayName, IReadOnlyList<string> PermissionNamespaces, bool Enabled, long Revision, bool IsAdministrative);
public sealed record ClientResourceGrantDto(Guid ResourceId, IReadOnlyList<string> DelegatedPermissions, IReadOnlyList<string> ApplicationPermissions, long Revision);
public sealed record ResourceConfiguration(string Audience, string Scope, string DisplayName, IReadOnlyList<string> PermissionNamespaces, bool Enabled = true, long? ExpectedRevision = null);
public sealed record ClientResourceGrantConfiguration(IReadOnlyList<string> DelegatedPermissions, IReadOnlyList<string> ApplicationPermissions, long? ExpectedRevision = null);

/// <summary>Manages business resource mappings and client ceilings behind the Applications.Write HTTP boundary.</summary>
public sealed class ResourceAccessWorkflow(IResourceAccessRepository resources, IApplicationRepository applications, IApplicationPermissionRegistryRepository registry)
{
    private static readonly DomainError conflict = DomainError.Conflict("ResourceAccess.Conflict", "Resource access changed; reload before saving.");
    public async Task<IReadOnlyList<ProtectedResourceDto>> ListResourcesAsync(CancellationToken cancellationToken = default) =>
        (await resources.ListResourcesAsync(cancellationToken)).Select(Map).ToArray();
    public async Task<IReadOnlyList<ClientResourceGrantDto>> ListGrantsAsync(Guid applicationId, CancellationToken cancellationToken = default) =>
        (await resources.ListGrantsAsync(new ApplicationId(applicationId), cancellationToken)).Select(Map).ToArray();

    public async Task<Result<ProtectedResourceDto>> SaveResourceAsync(Guid? id, ResourceConfiguration request, string actorId, CancellationToken cancellationToken = default)
    {
        if (request.PermissionNamespaces is null) { return ResourceAccessErrors.InvalidConfiguration; }
        ProtectedResource? resource = id is { } resourceId ? await resources.GetResourceAsync(resourceId, cancellationToken) : null;
        if (id is not null && resource is null) { return ResourceAccessErrors.UnknownResource; }
        if (resource?.IsAdministrative == true) { return ResourceAccessErrors.Reserved; }
        if (resource is not null && (request.Audience != resource.Audience || request.Scope != resource.Scope
            || request.ExpectedRevision != resource.Revision)) { return conflict; }
        foreach (string permissionNamespace in request.PermissionNamespaces)
        {
            if (string.Equals(permissionNamespace, ProtectedResource.PlatformNamespace, StringComparison.OrdinalIgnoreCase)) { return ResourceAccessErrors.Reserved; }
            if (await registry.GetByIdentifierAsync(permissionNamespace.ToLowerInvariant(), cancellationToken) is null) { return ResourceAccessErrors.InvalidConfiguration; }
        }

        if (resource is null)
        {
            if (await resources.FindByScopeAsync(request.Scope, cancellationToken) is not null
                || await resources.FindByAudienceAsync(request.Audience, cancellationToken) is not null) { return conflict; }
            Result<ProtectedResource> created = ProtectedResource.Create(request.Audience, request.Scope, request.DisplayName, request.PermissionNamespaces);
            if (created.IsFailure) { return created.Error; }
            resource = created.Value;
            resources.AddResource(resource);
        }

        Result configured = resource.Configure(request.DisplayName, request.PermissionNamespaces, request.Enabled);
        if (configured.IsFailure) { return configured.Error; }
        await resources.SaveChangesAsync(actorId, "ResourceMappingChanged", resource.Id.ToString(), resource, cancellationToken);
        return Map(resource);
    }

    public async Task<Result<ClientResourceGrantDto>> SaveGrantAsync(Guid applicationId, Guid resourceId, ClientResourceGrantConfiguration request, string actorId, CancellationToken cancellationToken = default)
    {
        if (request.DelegatedPermissions is null || request.ApplicationPermissions is null) { return ResourceAccessErrors.InvalidConfiguration; }
        var clientId = new ApplicationId(applicationId);
        if (await applications.GetByIdAsync(clientId, cancellationToken) is null) { return ResourceAccessErrors.NotGranted; }
        ProtectedResource? resource = await resources.GetResourceAsync(resourceId, cancellationToken);
        if (resource is null) { return ResourceAccessErrors.UnknownResource; }
        if (resource.IsAdministrative) { return ResourceAccessErrors.Reserved; }
        var candidates = new List<string>();
        foreach (string permissionNamespace in resource.PermissionNamespaces)
        {
            Domain.ApplicationPermissions.RegisteredApplication? catalog = await registry.GetByIdentifierAsync(permissionNamespace, cancellationToken);
            if (catalog is not null) { candidates.AddRange(catalog.Permissions.Where(static permission => !permission.IsRemoved).Select(static permission => permission.FullPermissionKey)); }
        }

        if (request.DelegatedPermissions.Concat(request.ApplicationPermissions).Any(permission =>
            !candidates.Any(candidate => PermissionSemantics.Matches(permission, candidate)))) { return ResourceAccessErrors.InvalidConfiguration; }
        ClientResourceGrant? grant = await resources.GetGrantAsync(clientId, resourceId, cancellationToken);
        if (grant is null)
        {
            if (request.ExpectedRevision is not null) { return conflict; }
            Result<ClientResourceGrant> created = ClientResourceGrant.Create(clientId, resourceId, request.DelegatedPermissions, request.ApplicationPermissions);
            if (created.IsFailure) { return created.Error; }
            grant = created.Value;
            resources.AddGrant(grant);
        }
        else
        {
            if (request.ExpectedRevision != grant.Revision) { return conflict; }
            Result configured = grant.Configure(request.DelegatedPermissions, request.ApplicationPermissions);
            if (configured.IsFailure) { return configured.Error; }
        }

        await resources.SaveChangesAsync(actorId, "ClientResourceGrantChanged", grant.Id.ToString(), cancellationToken: cancellationToken);
        return Map(grant);
    }

    private static ProtectedResourceDto Map(ProtectedResource resource) => new(resource.Id, resource.Audience, resource.Scope, resource.DisplayName, resource.PermissionNamespaces, resource.Enabled, resource.Revision, resource.IsAdministrative);
    private static ClientResourceGrantDto Map(ClientResourceGrant grant) => new(grant.ResourceId, grant.DelegatedPermissions, grant.ApplicationPermissions, grant.Revision);
}
