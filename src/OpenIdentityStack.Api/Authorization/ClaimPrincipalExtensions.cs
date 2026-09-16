using System.Security.Claims;

namespace OpenIdentityStack.Api.Authorization;

/// <summary>
/// Allocation-free claim lookups for per-request authorization paths. The built-in helpers
/// materialize every matching claim, which is wasteful when a single value is expected.
/// </summary>
internal static class ClaimPrincipalExtensions
{
    /// <summary>
    /// Returns the only claim of <paramref name="claimType"/>, or <c>null</c> when it is absent or
    /// duplicated. Duplicates collapse into the same result as absence because ambiguity must not be
    /// resolved by taking the first value.
    /// </summary>
    /// <remarks>
    /// Collapsing duplicates into <c>null</c> is only safe when the caller treats absence the same
    /// way. A caller that must reject duplicates has to count the claims itself by iterating
    /// <see cref="ClaimsPrincipal.FindAll(string)"/>, because there is no way to tell the two cases
    /// apart from this result.
    /// </remarks>
    public static Claim? FindSingleClaim(this ClaimsPrincipal principal, string claimType)
    {
        Claim? match = null;
        foreach (Claim claim in principal.FindAll(claimType))
        {
            if (match is not null)
            {
                return null;
            }

            match = claim;
        }

        return match;
    }

    /// <summary>
    /// Returns whether any <c>scope</c> claim lists <paramref name="scope"/> as a whole,
    /// space-delimited value, matching the splitting rules of the scope format.
    /// </summary>
    public static bool ContainsScopeValue(this ClaimsPrincipal principal, string scope)
    {
        foreach (Claim claim in principal.FindAll("scope"))
        {
            ReadOnlySpan<char> remaining = claim.Value.AsSpan();
            while (true)
            {
                int separator = remaining.IndexOf(' ');
                ReadOnlySpan<char> candidate = separator < 0 ? remaining : remaining[..separator];
                if (!candidate.IsEmpty && candidate.Equals(scope, StringComparison.Ordinal))
                {
                    return true;
                }

                if (separator < 0)
                {
                    break;
                }

                remaining = remaining[(separator + 1)..];
            }
        }

        return false;
    }
}
