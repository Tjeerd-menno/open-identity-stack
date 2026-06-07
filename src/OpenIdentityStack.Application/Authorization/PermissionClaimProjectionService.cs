using System.Security.Claims;

using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.Authorization;

public sealed class PermissionClaimProjectionService : IPermissionClaimProjectionService
{
    private readonly IApplicationPermissionRegistryRepository? applicationPermissionRegistryRepository;

    public PermissionClaimProjectionService(IApplicationPermissionRegistryRepository? applicationPermissionRegistryRepository = null)
    {
        this.applicationPermissionRegistryRepository = applicationPermissionRegistryRepository;
    }

    public async Task<IReadOnlyList<string>> ExpandAssignedPermissionsAsync(
        IEnumerable<string> assignedPermissions,
        CancellationToken cancellationToken = default)
    {
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (string assignedPermission in assignedPermissions)
        {
            string normalized = assignedPermission.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (!normalized.Contains('*', StringComparison.Ordinal))
            {
                if (emitted.Add(normalized))
                {
                    result.Add(normalized);
                }

                continue;
            }

            IReadOnlyList<string> expanded = await this.ExpandWildcardPermissionAsync(normalized, cancellationToken)
                .ConfigureAwait(false);

            if (expanded.Count == 0)
            {
                throw new InvalidOperationException($"Permission wildcard '{normalized}' could not be expanded.");
            }

            foreach (string permission in expanded)
            {
                if (emitted.Add(permission))
                {
                    result.Add(permission);
                }
            }
        }

        return result;
    }

    public IReadOnlyList<string> FilterPermissionsForCaller(
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
            string normalized = permission.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized)
                || !IsPermissionRelevantToCaller(normalized, requestingClientId)
                || !seen.Add(normalized))
            {
                continue;
            }

            filtered.Add(normalized);
        }

        return filtered;
    }

    IReadOnlyList<string> IPermissionClaimProjectionService.GetPermissionClaims(ClaimsPrincipal principal) =>
        GetPermissionClaims(principal).ToList();

    public static IEnumerable<string> GetPermissionClaims(ClaimsPrincipal principal) =>
        principal.FindAll("permission")
            .Concat(principal.FindAll("permissions"))
            .SelectMany(static claim => claim.Value.Split(
                [' ', ','],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private async Task<IReadOnlyList<string>> ExpandWildcardPermissionAsync(
        string normalizedPermission,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> platformPermissions = normalizedPermission == Permissions.All
            ? Permissions.GetAllPermissions()
            : Permissions.GetAllPermissions()
                .Where(requiredPermission => Permissions.Matches(normalizedPermission, requiredPermission))
                .ToList();

        if (platformPermissions.Count > 0)
        {
            return platformPermissions;
        }

        string[] parts = normalizedPermission.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || parts[2] != "*" || this.applicationPermissionRegistryRepository is null)
        {
            return [];
        }

        RegisteredApplication? application = await this.applicationPermissionRegistryRepository
            .GetByIdentifierAsync(parts[0], cancellationToken)
            .ConfigureAwait(false);

        if (application is null || application.Status != ApplicationLifecycleStatus.Active)
        {
            return [];
        }

        string prefix = $"{parts[1]}:";
        return application.Permissions
            .Where(permission => !permission.IsRemoved)
            .Where(permission => permission.PermissionKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(permission => permission.FullPermissionKey)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsPermissionRelevantToCaller(string permission, string requestingClientId) =>
        string.Equals(permission, Permissions.All, StringComparison.Ordinal)
        || permission.StartsWith($"{requestingClientId}:", StringComparison.OrdinalIgnoreCase);
}
