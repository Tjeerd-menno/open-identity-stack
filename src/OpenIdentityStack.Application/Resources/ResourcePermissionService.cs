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

public sealed class ResourcePermissionService : IResourcePermissionService
{
    private readonly IResourceAccessRepository resources;
    private readonly IApplicationRepository applications;
    private readonly IApplicationPermissionRegistryRepository registry;
    private readonly IUserRepository users;
    private readonly IGetUserEffectiveRolesQueryHandler roles;
    private readonly AdministrativeAccessDecisionCache cache;

    internal ResourcePermissionService(
        IResourceAccessRepository resources,
        IApplicationRepository applications,
        IApplicationPermissionRegistryRepository registry,
        IUserRepository users,
        IGetUserEffectiveRolesQueryHandler roles,
        AdministrativeAccessDecisionCache? cache = null)
    {
        this.resources = resources;
        this.applications = applications;
        this.registry = registry;
        this.users = users;
        this.roles = roles;
        this.cache = cache ?? new AdministrativeAccessDecisionCache();
    }

    public async Task<Result<ResourceTokenProjection>> ProjectAsync(ResourceTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (await this.FindCachedAdministrativeProjectionAsync(request, cancellationToken) is { } cached)
        {
            return cached;
        }

        ClientTokenProjection? client = await applications.GetTokenProjectionAsync(request.ClientId, cancellationToken);
        if (client is null || client.Status != ApplicationStatus.Active)
        {
            return ResourceAccessErrors.NotGranted;
        }

        string[] requestedScopes = request.Scopes.Where(static scope => !ProtectedResource.IsProtocolScope(scope)).Distinct(StringComparer.Ordinal).ToArray();
        var requested = new Dictionary<Guid, ProtectedResource>();
        if (requestedScopes.Length > 0)
        {
            var resourcesByScope = (await resources.FindByScopesAsync(requestedScopes, cancellationToken))
                .ToDictionary(static resource => resource.Scope, StringComparer.Ordinal);
            foreach (string scope in requestedScopes)
            {
                if (!resourcesByScope.TryGetValue(scope, out ProtectedResource? resource) || !resource.Enabled) { return ResourceAccessErrors.UnknownResource; }
                requested[resource.Id] = resource;
            }
        }

        if (request.RequestedResources.Count > 0)
        {
            string[] audiences = request.RequestedResources.Distinct(StringComparer.Ordinal).ToArray();
            var resourcesByAudience = (await resources.FindByAudiencesAsync(audiences, cancellationToken))
                .ToDictionary(static resource => resource.Audience, StringComparer.Ordinal);
            var selected = new HashSet<Guid>();
            foreach (string audience in audiences)
            {
                if (!resourcesByAudience.TryGetValue(audience, out ProtectedResource? resource) || !resource.Enabled || !requested.ContainsKey(resource.Id))
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
            if (request.UserPermissions is not null)
            {
                // Caller already resolved the user's effective roles for this request; reuse them
                // instead of re-running the roles + groups + mapped-role queries.
                userPermissions = request.UserPermissions;
            }
            else
            {
                Result<IReadOnlyList<Roles.Queries.RoleDto>> effectiveRoles = await roles.HandleAsync(userId, cancellationToken);
                if (effectiveRoles.IsFailure) { return ResourceAccessErrors.NotGranted; }
                userPermissions = effectiveRoles.Value.Where(static role => role.IsActive).SelectMany(static role => role.Permissions).ToArray();
            }
        }

        // Index the caller's permissions once instead of re-scanning them with PermissionSemantics.Matches
        // for every candidate permission of every resource.
        PermissionGrantIndex? userPermissionIndex = request.UserId is null ? null : PermissionGrantIndex.Create(userPermissions);

        // Namespaces repeat across resources, so resolve each registry catalog at most once per projection.
        var catalogs = new Dictionary<string, IReadOnlyList<string>?>(StringComparer.Ordinal);
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        var revisions = new Dictionary<Guid, long>();
        Dictionary<Guid, ClientResourceGrant>? grantsByResource = null;
        foreach (ProtectedResource resource in requested.Values)
        {
            if (!client.AllowedScopes.Contains(resource.Scope, StringComparer.Ordinal)) { return ResourceAccessErrors.NotGranted; }
            grantsByResource ??= (await resources.ListGrantsAsync(client.Id, cancellationToken)).ToDictionary(static grant => grant.ResourceId);
            if (!grantsByResource.TryGetValue(resource.Id, out ClientResourceGrant? grant)) { return ResourceAccessErrors.NotGranted; }
            IReadOnlyList<string> assigned = request.UserId is null ? grant.ApplicationPermissions : grant.DelegatedPermissions;
            if (assigned.Count == 0) { return ResourceAccessErrors.NotGranted; }
            revisions[resource.Id] = grant.Revision;
            var assignedIndex = PermissionGrantIndex.Create(assigned);
            foreach (string permissionNamespace in resource.PermissionNamespaces)
            {
                IReadOnlyList<string>? candidates;
                if (resource.IsAdministrative && permissionNamespace == ProtectedResource.PlatformNamespace)
                {
                    candidates = Permissions.GetAllPermissions();
                }
                else if (!catalogs.TryGetValue(permissionNamespace, out candidates))
                {
                    RegisteredApplication? catalog = await registry.GetByIdentifierAsync(permissionNamespace, cancellationToken);
                    candidates = catalog is null || catalog.Status != ApplicationLifecycleStatus.Active
                        ? null
                        : catalog.Permissions.Where(static permission => !permission.IsRemoved).Select(static permission => permission.FullPermissionKey).ToArray();
                    catalogs[permissionNamespace] = candidates;
                }

                if (candidates is null) { continue; }

                foreach (string candidate in candidates)
                {
                    if (assignedIndex.Covers(candidate)
                        && (userPermissionIndex is null || userPermissionIndex.Covers(candidate))
                        && (request.OriginalPermissions is null || request.OriginalPermissions.Contains(candidate, StringComparer.Ordinal)))
                    {
                        permissions.Add(candidate);
                    }
                }
            }
        }

        if (permissions.Count == 0 && requested.Values.Any(static resource => resource.IsAdministrative))
        {
            return ResourceAccessErrors.NotGranted;
        }

        var projection = new ResourceTokenProjection(requested.Values.Select(static resource => resource.Audience).Order(StringComparer.Ordinal).ToArray(),
            permissions.Order(StringComparer.Ordinal).ToArray(), revisions);
        this.RememberAdministrativeProjection(request, projection);
        return projection;
    }

    private async Task<ResourceTokenProjection?> FindCachedAdministrativeProjectionAsync(
        ResourceTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAdministrativeCacheKey(request, out long revision, out string fingerprint)
            || this.cache.Find(revision, fingerprint) is not { } cached)
        {
            return null;
        }

        // The revision row can still be read when the administrative resource table cannot.
        // Re-read that resource through the batched scope lookup so a store outage fails the
        // request instead of replaying a cached allow.
        ProtectedResource? resource = (await this.resources.FindByScopesAsync([ProtectedResource.AdministrativeScope], cancellationToken))
            .SingleOrDefault(candidate => string.Equals(candidate.Scope, ProtectedResource.AdministrativeScope, StringComparison.Ordinal));
        if (resource is { Enabled: true }
            && string.Equals(resource.Audience, ProtectedResource.AdministrativeAudience, StringComparison.Ordinal))
        {
            return cached;
        }

        this.cache.Evict(revision, fingerprint);
        return null;
    }

    private void RememberAdministrativeProjection(ResourceTokenRequest request, ResourceTokenProjection projection)
    {
        if (!TryGetAdministrativeCacheKey(request, out long revision, out string fingerprint))
        {
            return;
        }

        this.cache.Store(revision, fingerprint, new ResourceTokenProjection(
            projection.Audiences.ToArray(),
            projection.Permissions.ToArray(),
            new Dictionary<Guid, long>(projection.GrantRevisions)));
    }

    /// <summary>
    /// Administrative calls pass the revision just captured for the request.
    /// Token issuance leaves it unset and always reads current authority.
    /// Caller-supplied user permissions are not cached: they skip the live role query.
    /// </summary>
    private static bool TryGetAdministrativeCacheKey(ResourceTokenRequest request, out long revision, out string fingerprint)
    {
        fingerprint = string.Empty;
        if (request.AuthorityRevision is not long authorityRevision
            || request.UserPermissions is not null
            || request.Scopes is not [ProtectedResource.AdministrativeScope]
            || request.RequestedResources is not [ProtectedResource.AdministrativeAudience]
            || request.OriginalAudiences is not (null or [ProtectedResource.AdministrativeAudience]))
        {
            revision = 0;
            return false;
        }

        revision = authorityRevision;
        fingerprint = string.Join(
            '\u001f',
            request.ClientId,
            request.UserId is { } subject ? subject.Value.ToString("D") : "application",
            Ceiling(request.OriginalPermissions));
        return true;
    }

    private static string Ceiling(IReadOnlyList<string>? values) => values is null
        ? "\u0000"
        : string.Join('\u001e', values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
}
