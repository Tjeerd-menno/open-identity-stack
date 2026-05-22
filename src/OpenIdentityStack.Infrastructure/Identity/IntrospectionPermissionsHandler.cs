using System.Collections.Immutable;
using System.Security.Claims;

using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Queries;

using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Identity;

internal sealed class IntrospectionPermissionsHandler :
    IOpenIddictServerHandler<OpenIddictServerEvents.HandleIntrospectionRequestContext>
{
    private readonly IGetUserEffectiveRolesQueryHandler getUserEffectiveRolesQueryHandler;

    public IntrospectionPermissionsHandler(IGetUserEffectiveRolesQueryHandler getUserEffectiveRolesQueryHandler)
    {
        this.getUserEffectiveRolesQueryHandler = getUserEffectiveRolesQueryHandler;
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
            permissions.AddRange(GetPermissionClaims(principal));
        }

        return FilterPermissionsForCaller(permissions, requestingClientId);
    }

    private static List<string> FilterPermissionsForCaller(
        IEnumerable<string> permissions,
        string? requestingClientId)
    {
        if (string.IsNullOrWhiteSpace(requestingClientId))
        {
            return [];
        }

        var filtered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string permission in permissions)
        {
            if (!IsPermissionRelevantToCaller(permission, requestingClientId)
                || !seen.Add(permission))
            {
                continue;
            }

            filtered.Add(permission);
        }

        return filtered;
    }

    private static bool IsPermissionRelevantToCaller(string permission, string requestingClientId) =>
        permission.StartsWith($"{requestingClientId}:", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> GetPermissionClaims(ClaimsPrincipal principal) =>
        principal.FindAll("permission")
            .Concat(principal.FindAll("permissions"))
            .SelectMany(static claim => claim.Value.Split(
                [' ', ','],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
