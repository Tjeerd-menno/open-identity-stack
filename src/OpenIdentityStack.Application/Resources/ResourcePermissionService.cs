using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.ApplicationPermissions;
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Application.Resources;

public sealed class ResourcePermissionService(
    IResourceAccessRepository resources,
    IApplicationRepository applications,
    IApplicationPermissionRegistryRepository registry,
    IUserRepository users,
    IGetUserEffectiveRolesQueryHandler roles) : IResourcePermissionService
{
    public async Task<Result<ResourceTokenProjection>> ProjectAsync(ResourceTokenRequest request, CancellationToken cancellationToken = default)
    {
        Domain.Applications.Application? client = await applications.GetByClientIdAsync(request.ClientId, cancellationToken);
        if (client is null || client.Status != ApplicationStatus.Active)
        {
            return ResourceAccessErrors.NotGranted;
        }

        var requested = new Dictionary<Guid, ProtectedResource>();
        foreach (string scope in request.Scopes.Distinct(StringComparer.Ordinal))
        {
            if (ProtectedResource.IsProtocolScope(scope)) { continue; }
            ProtectedResource? resource = await resources.FindByScopeAsync(scope, cancellationToken);
            if (resource is null || !resource.Enabled) { return ResourceAccessErrors.UnknownResource; }
            requested[resource.Id] = resource;
        }

        if (request.RequestedResources.Count > 0)
        {
            var selected = new HashSet<Guid>();
            foreach (string audience in request.RequestedResources.Distinct(StringComparer.Ordinal))
            {
                ProtectedResource? resource = await resources.FindByAudienceAsync(audience, cancellationToken);
                if (resource is null || !resource.Enabled || !requested.ContainsKey(resource.Id))
                {
                    return ResourceAccessErrors.UnknownResource;
                }

                selected.Add(resource.Id);
            }

            // Every resource-bearing scope must have an unambiguous requested audience.
            if (!selected.SetEquals(requested.Keys)) { return ResourceAccessErrors.UnknownResource; }
        }

        if (requested.Count > 1 && requested.Values.Any(static resource => resource.IsAdministrative))
        {
            return ResourceAccessErrors.UnknownResource;
        }

        if (request.OriginalAudiences is not null && requested.Values.Any(resource => !request.OriginalAudiences.Contains(resource.Audience, StringComparer.Ordinal)))
        {
            return ResourceAccessErrors.NotGranted;
        }

        IReadOnlyList<string> userPermissions = [];
        if (request.UserId is { } userId)
        {
            User? user = await users.GetByIdAsync(userId, cancellationToken);
            if (user is null || user.Status != UserStatus.Active) { return ResourceAccessErrors.NotGranted; }
            Result<IReadOnlyList<Roles.Queries.RoleDto>> effectiveRoles = await roles.HandleAsync(userId, cancellationToken);
            if (effectiveRoles.IsFailure) { return ResourceAccessErrors.NotGranted; }
            userPermissions = effectiveRoles.Value.Where(static role => role.IsActive).SelectMany(static role => role.Permissions).ToArray();
        }

        var permissions = new HashSet<string>(StringComparer.Ordinal);
        var revisions = new Dictionary<Guid, long>();
        foreach (ProtectedResource resource in requested.Values)
        {
            if (!client.AllowedScopes.Contains(resource.Scope, StringComparer.Ordinal)) { return ResourceAccessErrors.NotGranted; }
            ClientResourceGrant? grant = await resources.GetGrantAsync(client.Id, resource.Id, cancellationToken);
            if (grant is null) { return ResourceAccessErrors.NotGranted; }
            IReadOnlyList<string> assigned = request.UserId is null ? grant.ApplicationPermissions : grant.DelegatedPermissions;
            if (assigned.Count == 0) { return ResourceAccessErrors.NotGranted; }
            revisions[resource.Id] = grant.Revision;
            foreach (string permissionNamespace in resource.PermissionNamespaces)
            {
                IReadOnlyList<string> candidates;
                if (resource.IsAdministrative && permissionNamespace == ProtectedResource.PlatformNamespace)
                {
                    candidates = Permissions.GetAllPermissions();
                }
                else
                {
                    RegisteredApplication? catalog = await registry.GetByIdentifierAsync(permissionNamespace, cancellationToken);
                    if (catalog is null || catalog.Status != ApplicationLifecycleStatus.Active) { continue; }
                    candidates = catalog.Permissions.Where(static permission => !permission.IsRemoved).Select(static permission => permission.FullPermissionKey).ToArray();
                }

                foreach (string candidate in candidates)
                {
                    if (assigned.Any(permission => PermissionSemantics.Matches(permission, candidate))
                        && (request.UserId is null || userPermissions.Any(permission => PermissionSemantics.Matches(permission, candidate)))
                        && (request.OriginalPermissions is null || request.OriginalPermissions.Contains(candidate, StringComparer.Ordinal)))
                    {
                        permissions.Add(candidate);
                    }
                }
            }
        }

        return new ResourceTokenProjection(requested.Values.Select(static resource => resource.Audience).Order(StringComparer.Ordinal).ToArray(),
            permissions.Order(StringComparer.Ordinal).ToArray(), revisions);
    }
}
