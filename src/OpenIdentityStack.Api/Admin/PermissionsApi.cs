using Microsoft.AspNetCore.Http.HttpResults;
using OpenIdentityStack.Api.Admin.Responses.ApplicationPermissions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;

namespace OpenIdentityStack.Api.Admin;

internal static class PermissionsApi
{
    public static IEndpointRouteBuilder MapPermissionsApi(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/admin/permissions")
            .WithTags(nameof(PermissionsApi));

        group.MapGet("platform", ListPlatformPermissions)
            .RequireAuthorization(Permissions.Roles.Read)
            .Produces<PlatformPermissionCatalogResponse>(StatusCodes.Status200OK)
            .WithName("ListPlatformPermissionCatalog")
            .WithSummary("Lists assignable platform permissions");

        return app;
    }

    private static Ok<PlatformPermissionCatalogResponse> ListPlatformPermissions()
    {
        var concreteItems = Permissions.GetAllPermissions()
            .Select(permission =>
            {
                string[] parts = permission.Split(':', 2);
                string resource = parts[0];
                string action = parts.Length == 2 ? parts[1] : string.Empty;
                return new PlatformPermissionCatalogItemDto(
                    permission,
                    resource,
                    action,
                    "concrete",
                    ToDisplayName(resource, action),
                    true);
            })
            .ToList();

        var wildcardItems = concreteItems
            .GroupBy(item => item.Resource)
            .Select(group => new PlatformPermissionCatalogItemDto(
                $"{group.Key}:*",
                group.Key,
                "*",
                "wildcard",
                $"{ToTitle(group.Key)} All",
                true))
            .ToList();

        var allWildcard = new PlatformPermissionCatalogItemDto(
            Permissions.All,
            "*",
            "*",
            "wildcard",
            "All Platform Permissions",
            true);

        var items = concreteItems
            .Concat(wildcardItems)
            .Append(allWildcard)
            .OrderBy(item => item.Resource, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Kind == "wildcard" ? 0 : 1)
            .ThenBy(item => item.Permission, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return TypedResults.Ok(new PlatformPermissionCatalogResponse(items));
    }

    private static string ToDisplayName(string resource, string action)
    {
        return $"{ToTitle(action)} {ToTitle(resource)}".Trim();
    }

    private static string ToTitle(string value)
    {
        if (value == "*")
        {
            return "All";
        }

        return string.Join(' ', value.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => string.Concat(part[..1].ToUpperInvariant(), part[1..])));
    }
}
