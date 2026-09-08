using System.Security.Claims;

using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Queries;
using SharedKernel;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenIdentityStack.Api.Authentication;

/// <summary>
/// Replaces authorization claims from an issued token with the user's current effective grants.
/// </summary>
public sealed class CurrentAuthorizationClaimsProjector
{
    private readonly IGetUserEffectiveRolesQueryHandler rolesQuery;
    private readonly IPermissionClaimProjectionService permissions;

    public CurrentAuthorizationClaimsProjector(
        IGetUserEffectiveRolesQueryHandler rolesQuery,
        IPermissionClaimProjectionService permissions)
    {
        this.rolesQuery = rolesQuery;
        this.permissions = permissions;
    }

    public async Task RefreshAsync(ClaimsPrincipal principal, UserId userId, CancellationToken cancellationToken = default)
    {
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

        ClaimsIdentity identity = principal.Identities.FirstOrDefault()
            ?? throw new InvalidOperationException("The token principal has no claims identity.");
        foreach (RoleDto role in result.Value)
        {
            identity.AddClaim(new Claim(Claims.Role, role.Name));
            IReadOnlyList<string> expanded = await this.permissions.ExpandAssignedPermissionsAsync(role.Permissions, cancellationToken);
            foreach (string permission in expanded)
            {
                identity.AddClaim(new Claim("permission", permission));
            }
        }
    }

    private static bool IsAuthorizationClaim(Claim claim) =>
        claim.Type is Claims.Role or ClaimTypes.Role or "permission" or "permissions";
}
