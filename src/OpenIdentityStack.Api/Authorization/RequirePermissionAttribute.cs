using System.Security.Claims;
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
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            return Task.CompletedTask;
        }

        HashSet<string> granted = CollectGrantedPermissions(context.User);

        if (granted.Any(value => Permissions.Matches(value, requirement.Permission)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static HashSet<string> CollectGrantedPermissions(ClaimsPrincipal user)
    {
        var granted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddClaims(granted, user, "permission");      // legacy/custom permission claim
        AddClaims(granted, user, "permissions");     // alternate naming
        AddScopes(granted, user, "scope");           // OAuth2/OIDC standard scope claim
        AddScopes(granted, user, "scp");             // Microsoft/JWT short scope claim

        foreach (Claim role in user.FindAll(ClaimTypes.Role).Concat(user.FindAll("role")))
        {
            if (string.Equals(role.Value, "admin", StringComparison.OrdinalIgnoreCase))
            {
                granted.Add(Permissions.All);
            }
        }

        return granted;
    }

    private static void AddClaims(HashSet<string> granted, ClaimsPrincipal user, string claimType)
    {
        foreach (Claim claim in user.FindAll(claimType))
        {
            if (!string.IsNullOrWhiteSpace(claim.Value))
            {
                granted.Add(claim.Value);
            }
        }
    }

    private static void AddScopes(HashSet<string> granted, ClaimsPrincipal user, string claimType)
    {
        foreach (Claim claim in user.FindAll(claimType))
        {
            if (string.IsNullOrWhiteSpace(claim.Value))
            {
                continue;
            }

            string[] scopes = claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string scope in scopes)
            {
                granted.Add(scope);
            }
        }
    }
}

/// <summary>
/// Authorization configuration helpers for permission policies.
/// </summary>
public static class AuthorizationOptionsExtensions
{
    public const string AdminPolicy = "AdminPolicy";

    /// <summary>
    /// Adds an authenticated-only admin policy and per-permission policies using PermissionRequirement.
    /// All policies use OpenIddict bearer token validation to prevent cookie-based login redirects.
    /// </summary>
    public static void AddPermissionPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(AdminPolicy, policy =>
        {
            policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
        });

        foreach (string permission in Permissions.GetAllPermissions())
        {
            options.AddPolicy(permission, policy =>
            {
                policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(permission));
            });
        }
    }
}
