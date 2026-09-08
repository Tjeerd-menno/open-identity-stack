using System.Security.Claims;

using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIddict.Abstractions;
using SharedKernel;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenIdentityStack.Api.Authentication;

/// <summary>
/// Replaces authorization claims from an issued token with the user's current effective grants.
/// </summary>
public sealed class CurrentAuthorizationClaimsProjector
{
    private readonly IGetUserEffectiveRolesQueryHandler rolesQuery;
    private readonly IResourcePermissionService resources;

    public CurrentAuthorizationClaimsProjector(
        IGetUserEffectiveRolesQueryHandler rolesQuery,
        IResourcePermissionService resources)
    {
        this.rolesQuery = rolesQuery;
        this.resources = resources;
    }

    public async Task RefreshAsync(ClaimsPrincipal principal, UserId userId, CancellationToken cancellationToken = default)
    {
        string clientId = principal.GetClaim("client_id") ?? string.Empty;
        string[] scopes = principal.GetScopes().ToArray();
        string[] audiences = (principal.GetAudiences().IsEmpty ? principal.GetResources() : principal.GetAudiences()).ToArray();
        string[] originalPermissions = principal.FindAll("permission").Select(static claim => claim.Value).ToArray();

        foreach (ClaimsIdentity existingIdentity in principal.Identities)
        {
            foreach (Claim claim in existingIdentity.Claims.Where(IsAuthorizationClaim).ToArray())
            {
                existingIdentity.RemoveClaim(claim);
            }
        }

        Result<IReadOnlyList<RoleDto>> result = await this.rolesQuery.HandleAsync(userId, cancellationToken);
        if (result.IsFailure)
        {
            return;
        }

        RoleDto[] activeRoles = result.Value.Where(static role => role.IsActive).ToArray();
        Result<ResourceTokenProjection> projection = await this.resources.ProjectAsync(
            new ResourceTokenRequest(clientId, scopes, audiences, userId, originalPermissions, audiences,
                activeRoles.SelectMany(static role => role.Permissions).ToArray()),
            cancellationToken);
        if (projection.IsFailure)
        {
            return;
        }

        ClaimsIdentity identity = principal.Identities.FirstOrDefault()
            ?? throw new InvalidOperationException("The token principal has no claims identity.");
        foreach (RoleDto role in activeRoles)
        {
            identity.AddClaim(new Claim(Claims.Role, role.Name));
        }

        foreach (string permission in projection.Value.Permissions)
        {
            identity.AddClaim(new Claim("permission", permission));
        }
    }

    private static bool IsAuthorizationClaim(Claim claim) =>
        claim.Type is Claims.Role or ClaimTypes.Role or "permission" or "permissions";
}
