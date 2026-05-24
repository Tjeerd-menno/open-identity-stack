namespace OpenIdentityStack.Application.ApplicationPermissions.Validators;

public static class ReservedApplicationPermissionNamespaces
{
    public static readonly IReadOnlySet<string> ReservedIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "users", "roles", "groups", "service-accounts", "sessions",
        "providers", "clients", "audit-logs", "system", "*"
    };

    public static bool IsReserved(string identifier) => ReservedIdentifiers.Contains(identifier);
}
