using System.Collections.Immutable;
using System.Security.Claims;

using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Queries;

using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Identity;

internal sealed class IntrospectionPermissionsHandler :
    IOpenIddictServerHandler<OpenIddictServerEvents.HandleIntrospectionRequestContext>
{
    private readonly IGetUserEffectiveRolesQueryHandler getUserEffectiveRolesQueryHandler;
    private readonly IPermissionClaimProjectionService permissionClaimProjectionService;

    public IntrospectionPermissionsHandler(
        IGetUserEffectiveRolesQueryHandler getUserEffectiveRolesQueryHandler,
        IPermissionClaimProjectionService? permissionClaimProjectionService = null,
        IApplicationPermissionRegistryRepository? applicationPermissionRegistryRepository = null)
    {
        this.getUserEffectiveRolesQueryHandler = getUserEffectiveRolesQueryHandler;
        this.permissionClaimProjectionService = permissionClaimProjectionService
            ?? new OpenIdentityStack.Application.Authorization.PermissionClaimProjectionService(applicationPermissionRegistryRepository);
    }

    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleIntrospectionRequestContext context)
    {
        string? requestingClientId = context.Request?.ClientId;
        string? subject = context.Subject ?? context.GenericTokenPrincipal?.GetClaim(OpenIddictConstants.Claims.Subject);

        IReadOnlyList<string> permissions = await this.ResolvePermissionsAsync(
            context.GenericTokenPrincipal,
            subject,
            requestingClientId,
            context.CancellationToken).ConfigureAwait(false);

        context.Claims["permissions"] = new OpenIddictParameter(
            permissions.Select(static permission => (string?)permission).ToImmutableArray());
    }

    private async Task<IReadOnlyList<string>> ResolvePermissionsAsync(
        ClaimsPrincipal? principal,
        string? subject,
        string? requestingClientId,
        CancellationToken cancellationToken)
    {
        var permissions = new List<string>();
        bool resolvedFromFreshRoles = false;

        if (Guid.TryParse(subject, out Guid userId))
        {
            Result<IReadOnlyList<RoleDto>> rolesResult =
                await this.getUserEffectiveRolesQueryHandler.HandleAsync(new UserId(userId), cancellationToken).ConfigureAwait(false);

            if (rolesResult.IsSuccess)
            {
                resolvedFromFreshRoles = true;
                foreach (RoleDto role in rolesResult.Value)
                {
                    permissions.AddRange(role.Permissions);
                }
            }
        }

        if (!resolvedFromFreshRoles && principal is not null)
        {
            permissions.AddRange(this.permissionClaimProjectionService.GetPermissionClaims(principal));
        }

        var expandedPermissions = new List<string>();
        foreach (string permission in permissions)
        {
            try
            {
                expandedPermissions.AddRange(await this.permissionClaimProjectionService
                    .ExpandAssignedPermissionsAsync([permission], cancellationToken)
                    .ConfigureAwait(false));
            }
            catch (InvalidOperationException)
            {
                // Introspection fails closed for an unexpandable wildcard without leaking the wildcard itself.
            }
        }

        return this.permissionClaimProjectionService.FilterPermissionsForCaller(expandedPermissions, requestingClientId);
    }
}
