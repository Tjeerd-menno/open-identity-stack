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

        ReadOnlySpan<char> granted = grantedPermission.AsSpan().Trim();
        ReadOnlySpan<char> required = requiredPermission.AsSpan().Trim();

        // Full wildcard only applies to platform permissions.
        if (granted.SequenceEqual(PlatformWildcard))
        {
            return CountColons(required) == 1;
        }

        if (granted.Equals(required, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IsTerminalWildcard(granted))
        {
            return false;
        }

        ReadOnlySpan<char> grantedResource = granted[..^2];
        return CountColons(required) == CountColons(grantedResource) + 1 &&
            required.Length > grantedResource.Length &&
            required.StartsWith(grantedResource, StringComparison.OrdinalIgnoreCase) &&
            required[grantedResource.Length] == ':';
    }

    internal static bool IsTerminalWildcard(ReadOnlySpan<char> grantedPermission) =>
        grantedPermission.Length >= 2 &&
        grantedPermission[^1] == '*' &&
        grantedPermission[^2] == ':' &&
        !grantedPermission[..^1].Contains('*');

    internal static int CountColons(ReadOnlySpan<char> value)
    {
        int count = 0;
        foreach (char character in value)
        {
            if (character == ':')
            {
                count++;
            }
        }

        return count;
    }
}
