namespace OpenIdentityStack.Domain.Groups;

/// <summary>Group attributes cannot supply identity, authorization, or token protocol fields.</summary>
public static class ReservedGroupClaimTypes
{
    private static readonly HashSet<string> reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "sub", "iss", "aud", "azp", "client_id", "scope", "scp", "permission", "permissions",
        "role", "roles", "auth_time", "amr", "acr", "sid", "session_id", "exp", "iat", "nbf", "jti",
        "email", "email_verified", "phone_number", "phone_number_verified", "ois_human_authenticated_at", "auth_method", "provider",
        "name", "given_name", "family_name", "middle_name", "nickname", "preferred_username", "profile", "picture",
        "website", "gender", "birthdate", "zoneinfo", "locale", "address", "updated_at",
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
        "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
    };

    public static bool IsReserved(string claimType)
    {
        claimType = claimType.Trim();
        return reserved.Contains(claimType) || claimType.StartsWith("oi_", StringComparison.OrdinalIgnoreCase) ||
        claimType.StartsWith("ois_", StringComparison.OrdinalIgnoreCase) || claimType.StartsWith("ois.", StringComparison.OrdinalIgnoreCase);
    }
}
