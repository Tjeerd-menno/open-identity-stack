namespace OpenIdentityStack.Application.Authorization;

/// <summary>
/// Defines all admin permissions in the format "resource:operation".
/// Permissions support wildcards:
/// - "*" matches all permissions
/// - "resource:*" matches all operations on a resource
/// </summary>
public static class Permissions
{
    /// <summary>
    /// Wildcard that grants all permissions.
    /// </summary>
    public const string All = "*";

    /// <summary>
    /// User management permissions.
    /// </summary>
    public static class Users
    {
        public const string Read = "users:read";
        public const string Write = "users:write";
        public const string Delete = "users:delete";
        public const string Disable = "users:disable";
        public const string ResetPassword = "users:reset-password";
        public const string All = "users:*";
    }

    /// <summary>
    /// Role management permissions.
    /// </summary>
    public static class Roles
    {
        public const string Read = "roles:read";
        public const string Write = "roles:write";
        public const string Delete = "roles:delete";
        public const string Assign = "roles:assign";
        public const string All = "roles:*";
    }

    /// <summary>
    /// Group management permissions.
    /// </summary>
    public static class Groups
    {
        public const string Read = "groups:read";
        public const string Write = "groups:write";
        public const string Delete = "groups:delete";
        public const string ManageMembers = "groups:manage-members";
        public const string All = "groups:*";
    }

    /// <summary>
    /// Service account management permissions.
    /// </summary>
    public static class ServiceAccounts
    {
        public const string Read = "service-accounts:read";
        public const string Write = "service-accounts:write";
        public const string Delete = "service-accounts:delete";
        public const string RotateSecret = "service-accounts:rotate-secret";
        public const string ManageCertificates = "service-accounts:manage-certificates";
        public const string All = "service-accounts:*";
    }

    /// <summary>
    /// Service permission registry permissions.
    /// </summary>
    public static class ServicePermissions
    {
        public const string Read = "service-permissions:read";
        public const string Write = "service-permissions:write";
        public const string Admin = "service-permissions:admin";
        public const string All = "service-permissions:*";
    }

    /// <summary>
    /// Session management permissions.
    /// </summary>
    public static class Sessions
    {
        public const string Read = "sessions:read";
        public const string Revoke = "sessions:revoke";
        public const string All = "sessions:*";
    }

    /// <summary>
    /// Identity provider management permissions.
    /// </summary>
    public static class Providers
    {
        public const string Read = "providers:read";
        public const string Write = "providers:write";
        public const string Delete = "providers:delete";
        public const string All = "providers:*";
    }

    /// <summary>
    /// Client application management permissions.
    /// </summary>
    public static class Clients
    {
        public const string Read = "clients:read";
        public const string Write = "clients:write";
        public const string Delete = "clients:delete";
        public const string ManageSecrets = "clients:manage-secrets";
        public const string All = "clients:*";
    }

    /// <summary>
    /// Audit log permissions.
    /// </summary>
    public static class AuditLogs
    {
        public const string Read = "audit-logs:read";
        public const string All = "audit-logs:*";
    }

    /// <summary>
    /// System administration permissions.
    /// </summary>
    public static class System
    {
        public const string ManageSettings = "system:settings";
        public const string ViewMetrics = "system:metrics";
        public const string All = "system:*";
    }

    /// <summary>
    /// Gets all defined permissions for documentation/seeding purposes.
    /// </summary>
    public static IReadOnlyList<string> GetAllPermissions() =>
    [
        Users.Read, Users.Write, Users.Delete, Users.Disable, Users.ResetPassword,
        Roles.Read, Roles.Write, Roles.Delete, Roles.Assign,
        Groups.Read, Groups.Write, Groups.Delete, Groups.ManageMembers,
        ServiceAccounts.Read, ServiceAccounts.Write, ServiceAccounts.Delete, 
        ServiceAccounts.RotateSecret, ServiceAccounts.ManageCertificates,
        ServicePermissions.Read, ServicePermissions.Write, ServicePermissions.Admin,
        Sessions.Read, Sessions.Revoke,
        Providers.Read, Providers.Write, Providers.Delete,
        Clients.Read, Clients.Write, Clients.Delete, Clients.ManageSecrets,
        AuditLogs.Read,
        System.ManageSettings, System.ViewMetrics
    ];

    /// <summary>
    /// Checks if the granted permission matches the required permission.
    /// Supports wildcards.
    /// </summary>
    /// <param name="grantedPermission">The permission granted to the user.</param>
    /// <param name="requiredPermission">The permission required for the action.</param>
    /// <returns>True if the granted permission covers the required permission.</returns>
    public static bool Matches(string grantedPermission, string requiredPermission)
    {
        if (string.IsNullOrEmpty(grantedPermission) || string.IsNullOrEmpty(requiredPermission))
        {
            return false;
        }

        // Full wildcard matches everything
        if (grantedPermission == All)
        {
            return true;
        }

        // Exact match
        if (string.Equals(grantedPermission, requiredPermission, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Resource wildcard match (e.g., "users:*" matches "users:read")
        if (grantedPermission.EndsWith(":*", StringComparison.OrdinalIgnoreCase))
        {
            string grantedResource = grantedPermission[..^2]; // Remove ":*"
            string[] requiredParts = requiredPermission.Split(':');
            if (requiredParts.Length >= 1 && 
                string.Equals(grantedResource, requiredParts[0], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
