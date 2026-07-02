namespace OpenIdentityStack.Application.Authorization;

/// <summary>
/// Executes the shared permission matching rules used by Admin API authorization and Management Web gating.
/// </summary>
public static class PermissionSemantics
{
    public const string PlatformWildcard = "*";

    /// <summary>
    /// Checks whether a granted permission covers a required permission.
    /// </summary>
    public static bool Matches(string grantedPermission, string requiredPermission)
    {
        if (string.IsNullOrWhiteSpace(grantedPermission) || string.IsNullOrWhiteSpace(requiredPermission))
        {
            return false;
        }

        string normalizedGranted = grantedPermission.Trim();
        string normalizedRequired = requiredPermission.Trim();
        string[] requiredParts = normalizedRequired.Split(':');

        // Full wildcard only applies to platform permissions.
        if (normalizedGranted == PlatformWildcard)
        {
            return requiredParts.Length == 2;
        }

        if (string.Equals(normalizedGranted, normalizedRequired, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IsTerminalWildcard(normalizedGranted))
        {
            return false;
        }

        string grantedResource = normalizedGranted[..^2];
        string[] grantedParts = grantedResource.Split(':');
        return requiredParts.Length == grantedParts.Length + 1 &&
            string.Equals(
                grantedResource,
                string.Join(':', requiredParts[..grantedParts.Length]),
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTerminalWildcard(string grantedPermission) =>
        grantedPermission.EndsWith(":*", StringComparison.OrdinalIgnoreCase) &&
        grantedPermission.IndexOf('*', StringComparison.OrdinalIgnoreCase) == grantedPermission.Length - 1;
}
