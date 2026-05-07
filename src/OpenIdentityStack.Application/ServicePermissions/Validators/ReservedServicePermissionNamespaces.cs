namespace OpenIdentityStack.Application.ServicePermissions.Validators;

public static class ReservedServicePermissionNamespaces
{
    public static readonly IReadOnlySet<string> ReservedIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "users", "roles", "groups", "service-accounts", "sessions",
        "providers", "clients", "audit-logs", "system", "*"
    };

    public static bool IsReserved(string identifier) => ReservedIdentifiers.Contains(identifier);
}
