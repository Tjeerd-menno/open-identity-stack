namespace OpenIdentityStack.Application.Authorization;

/// <summary>
/// Defines all admin permissions in the format "resource:operation".
/// Permissions support wildcards:
/// - "*" matches all platform permissions
/// - "resource:*" matches all operations on a platform resource
/// - "application:resource:*" matches all operations on a dynamic application resource
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
    /// Application permission registry permissions.
    /// </summary>
    public static class ApplicationPermissions
    {
        public const string Read = "application-permissions:read";
        public const string Write = "application-permissions:write";
        public const string Admin = "application-permissions:admin";
        public const string All = "application-permissions:*";
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
    /// Unified application management permissions.
    /// </summary>
    public static class Applications
    {
        public const string Read = "applications:read";
        public const string Write = "applications:write";
        public const string Delete = "applications:delete";
        public const string ManageCredentials = "applications:manage-credentials";
        public const string ManageCertificates = "applications:manage-certificates";
        public const string All = "applications:*";
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
        Applications.Read, Applications.Write, Applications.Delete,
        Applications.ManageCredentials, Applications.ManageCertificates,
        ApplicationPermissions.Read, ApplicationPermissions.Write, ApplicationPermissions.Admin,
        Sessions.Read, Sessions.Revoke,
        Providers.Read, Providers.Write, Providers.Delete,
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

        string[] requiredParts = requiredPermission.Split(':');

        // Full wildcard only applies to platform permissions.
        if (grantedPermission == All)
        {
            return requiredParts.Length == 2;
        }

        // Exact match
        if (string.Equals(grantedPermission, requiredPermission, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Terminal prefix wildcard matches exactly one trailing permission segment.
        if (grantedPermission.EndsWith(":*", StringComparison.OrdinalIgnoreCase) &&
            grantedPermission.IndexOf('*', StringComparison.OrdinalIgnoreCase) == grantedPermission.Length - 1)
        {
            string grantedResource = grantedPermission[..^2]; // Remove ":*"
            string[] grantedParts = grantedResource.Split(':');
            if (requiredParts.Length == grantedParts.Length + 1 &&
                string.Equals(
                    grantedResource,
                    string.Join(':', requiredParts[..grantedParts.Length]),
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
