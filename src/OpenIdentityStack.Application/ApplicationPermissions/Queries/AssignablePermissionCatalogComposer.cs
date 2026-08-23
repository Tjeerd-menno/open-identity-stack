using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;

namespace OpenIdentityStack.Application.ApplicationPermissions.Queries;

internal static class AssignablePermissionCatalogComposer
{
    public static PagedResult<ApplicationPermissionDto> Compose(
        IReadOnlyList<ApplicationPermissionDto> concreteItems,
        ListAssignablePermissionCatalogQuery query)
    {
        int page = Math.Max(query.Page, 1);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);

        var wildcardItems = concreteItems
            .GroupBy(
                item =>
                {
                    string[] keyParts = item.PermissionKey.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    return new
                    {
                        item.ApplicationId,
                        item.ApplicationName,
                        item.ApplicationVersion,
                        ResourceOrAggregate = keyParts.Length > 1 ? keyParts[0] : item.PermissionKey,
                    };
                })
            .Select(group => new ApplicationPermissionDto(
                Guid.Empty,
                $"{group.Key.ResourceOrAggregate}:*",
                $"{group.Key.ApplicationId}:{group.Key.ResourceOrAggregate}:*",
                $"{group.Key.ApplicationName} {ToTitle(group.Key.ResourceOrAggregate)} All",
                $"Grants all current {group.Key.ResourceOrAggregate} permissions for {group.Key.ApplicationName}.",
                group.Key.ResourceOrAggregate,
                DateTimeOffset.MinValue,
                null,
                group.Key.ApplicationId,
                group.Key.ApplicationName,
                group.Key.ApplicationVersion,
                "wildcard",
                true,
                group.Key.ResourceOrAggregate,
                group.Count()))
            .ToList();

        var allItems = concreteItems
            .Concat(wildcardItems)
            .OrderBy(item => item.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Kind == "wildcard" ? 0 : 1)
            .ThenBy(item => item.FullPermissionKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var pageItems = allItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return PagedResult<ApplicationPermissionDto>.Create(pageItems, page, pageSize, allItems.Count);
    }

    private static string ToTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return string.Join(' ', value.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => string.Concat(part[..1].ToUpperInvariant(), part.AsSpan(1))));
    }
}
