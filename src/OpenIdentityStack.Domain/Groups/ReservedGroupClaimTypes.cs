namespace OpenIdentityStack.Domain.Groups;

/// <summary>Group attributes cannot supply identity, authorization, or token protocol fields.</summary>
public static class ReservedGroupClaimTypes
{
    private static readonly HashSet<string> reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "sub", "iss", "aud", "azp", "client_id", "scope", "scp", "permission", "permissions",
        "role", "roles", "auth_time", "amr", "acr", "sid", "session_id", "exp", "iat", "nbf", "jti",
        "email", "email_verified", "ois_human_authenticated_at", "auth_method", "provider",
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
        "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
    };

    public static bool IsReserved(string claimType) =>
        reserved.Contains(claimType.Trim()) || claimType.StartsWith("oi_", StringComparison.OrdinalIgnoreCase) ||
        claimType.StartsWith("ois_", StringComparison.OrdinalIgnoreCase);
}
