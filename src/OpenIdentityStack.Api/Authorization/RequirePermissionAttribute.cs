using Microsoft.AspNetCore.Authorization;
using OpenIddict.Validation.AspNetCore;
using OpenIdentityStack.Application.Authorization;

namespace OpenIdentityStack.Api.Authorization;

/// <summary>
/// Authorization requirement that represents a single permission/scope.
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission)
    {
        this.Permission = permission ?? throw new ArgumentNullException(nameof(permission));
    }

    public string Permission { get; }
}

/// <summary>
/// Authorization handler that evaluates permission requirements against OAuth2/OIDC-aligned claims.
/// </summary>
public sealed class PermissionAuthorizationHandler(AdministrativeRequestAuthorization administrativeAccess) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        IReadOnlyList<string> permissions = await administrativeAccess.EvaluateAsync(context.User);
        if (permissions.Any(permission => Permissions.Matches(permission, requirement.Permission))) { context.Succeed(requirement); }
    }
}

public sealed class AdministrativeAccessRequirement : IAuthorizationRequirement;

public sealed class AdministrativeAccessAuthorizationHandler(AdministrativeRequestAuthorization administrativeAccess) : AuthorizationHandler<AdministrativeAccessRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdministrativeAccessRequirement requirement)
    {
        if ((await administrativeAccess.EvaluateAsync(context.User)).Count > 0) { context.Succeed(requirement); }
    }
}

/// <summary>
/// Authorization configuration helpers for permission policies.
/// </summary>
public static class AuthorizationOptionsExtensions
{
    public const string AdminPolicy = "AdminPolicy";

    /// <summary>
    /// Adds the dedicated administrative audience/entitlement boundary and per-permission policies.
    /// All policies use OpenIddict bearer token validation to prevent cookie-based login redirects.
    /// </summary>
    public static void AddPermissionPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(AdminPolicy, policy =>
        {
            policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new AdministrativeGrantRevisionRequirement());
            policy.AddRequirements(new AdministrativeAccessRequirement());
        });

        foreach (string permission in Permissions.GetAllPermissions())
        {
            options.AddPolicy(permission, policy =>
            {
                policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new AdministrativeGrantRevisionRequirement());
                policy.AddRequirements(new AdministrativeAccessRequirement());
                policy.AddRequirements(new PermissionRequirement(permission));
            });
        }
    }
}
