namespace OpenIdentityStack.Application.Authorization;

/// <summary>
/// Indexes a set of granted permissions so that candidates can be tested for coverage without
/// re-scanning every grant with <see cref="PermissionSemantics.Matches"/>. Semantics are identical
/// to <c>grants.Any(granted =&gt; PermissionSemantics.Matches(granted, required))</c>; see
/// <c>PermissionGrantIndexTests</c> for the equivalence matrix.
/// </summary>
public sealed class PermissionGrantIndex
{
    private readonly HashSet<string> exact;
    private readonly List<string> namespaceWildcards;
    private readonly bool grantsAllPlatformPermissions;

    private PermissionGrantIndex(HashSet<string> exact, List<string> namespaceWildcards, bool grantsAllPlatformPermissions)
    {
        this.exact = exact;
        this.namespaceWildcards = namespaceWildcards;
        this.grantsAllPlatformPermissions = grantsAllPlatformPermissions;
    }

    /// <summary>Builds an index over a set of granted permissions. Blank entries are ignored.</summary>
    public static PermissionGrantIndex Create(IEnumerable<string> grantedPermissions)
    {
        var exact = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var namespaceWildcards = new List<string>();
        bool grantsAll = false;

        if (grantedPermissions is not null)
        {
            foreach (string grantedPermission in grantedPermissions)
            {
                if (string.IsNullOrWhiteSpace(grantedPermission))
                {
                    continue;
                }

                ReadOnlySpan<char> granted = grantedPermission.AsSpan().Trim();
                if (granted.SequenceEqual(PermissionSemantics.PlatformWildcard))
                {
                    grantsAll = true;
                    continue;
                }

                if (PermissionSemantics.IsTerminalWildcard(granted))
                {
                    namespaceWildcards.Add(granted[..^2].ToString());
                    continue;
                }

                exact.Add(granted.ToString());
            }
        }

        return new PermissionGrantIndex(exact, namespaceWildcards, grantsAll);
    }

    /// <summary>Checks whether any indexed grant covers the required permission.</summary>
    public bool Covers(string requiredPermission)
    {
        if (string.IsNullOrWhiteSpace(requiredPermission))
        {
            return false;
        }

        ReadOnlySpan<char> required = requiredPermission.AsSpan().Trim();

        // The full wildcard only covers platform permissions, which carry exactly one separator.
        if (this.grantsAllPlatformPermissions && PermissionSemantics.CountColons(required) == 1)
        {
            return true;
        }

        // Trimming is a no-op for already normalized values, which is the common case, so the
        // lookup can reuse the caller's string instead of materializing a slice.
        if (required.Length == requiredPermission.Length)
        {
            if (this.exact.Contains(requiredPermission))
            {
                return true;
            }
        }
        else if (this.exact.Contains(required.ToString()))
        {
            return true;
        }

        foreach (string grantedResource in this.namespaceWildcards)
        {
            if (PermissionSemantics.CountColons(required) == PermissionSemantics.CountColons(grantedResource) + 1 &&
                required.Length > grantedResource.Length &&
                required.StartsWith(grantedResource, StringComparison.OrdinalIgnoreCase) &&
                required[grantedResource.Length] == ':')
            {
                return true;
            }
        }

        return false;
    }
}
