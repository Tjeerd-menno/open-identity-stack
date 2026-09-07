
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Persistence;
/// <summary>
/// Seeds initial data into the database, including system roles.
/// </summary>
public static partial class SeedData
{
    /// <summary>
    /// Well-known system role names.
    /// </summary>
    public static class SystemRoles
    {
        /// <summary>
        /// Super Admin role with full permissions.
        /// </summary>
        public const string SuperAdmin = "super-admin";

        /// <summary>
        /// User Administrator role with user management permissions.
        /// </summary>
        public const string UserAdmin = "user-admin";

        /// <summary>
        /// Session Administrator role with session management permissions.
        /// </summary>
        public const string SessionAdmin = "session-admin";

        /// <summary>
        /// Audit Viewer role with read-only access to audit logs.
        /// </summary>
        public const string AuditViewer = "audit-viewer";

        /// <summary>
        /// Application Administrator role with full unified application management permissions.
        /// </summary>
        public const string ApplicationAdmin = "application-admin";
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "System role '{RoleName}' already exists, skipping")]
    private static partial void LogRoleAlreadyExists(ILogger logger, string roleName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to create system role '{RoleName}': {Error}")]
    private static partial void LogRoleCreationFailed(ILogger logger, string roleName, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created system role '{RoleName}' with {PermissionCount} permissions")]
    private static partial void LogRoleCreated(ILogger logger, string roleName, int permissionCount);

    /// <summary>
    /// Seeds the database with initial data.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task SeedAsync(
        OpenIdentityStackDbContext context,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        await SeedSystemRolesAsync(context, logger, cancellationToken);
    }

    private static async Task SeedSystemRolesAsync(
        OpenIdentityStackDbContext context,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var systemRoles = new List<(string Name, string DisplayName, string Description, string[] Permissions)>
        {
            (
                SystemRoles.SuperAdmin,
                "Super Administrator",
                "Full administrative access to all system features",
                [Permissions.All]
            ),
            (
                SystemRoles.UserAdmin,
                "User Administrator",
                "Manage users, roles, and groups",
                [
                    Permissions.Users.All,
                    Permissions.Roles.Read,
                    Permissions.Roles.Assign,
                    Permissions.Groups.All
                ]
            ),
            (
                SystemRoles.ApplicationAdmin,
                "Application Administrator",
                "Manage OAuth/OIDC applications and credentials",
                [
                    Permissions.Applications.All
                ]
            ),
            (
                SystemRoles.SessionAdmin,
                "Session Administrator",
                "View and revoke user sessions",
                [
                    Permissions.Sessions.All,
                    Permissions.Users.Read
                ]
            ),
            (
                SystemRoles.AuditViewer,
                "Audit Viewer",
                "Read-only access to audit logs",
                [
                    Permissions.AuditLogs.Read
                ]
            )
        };

        foreach ((string? name, string? displayName, string? description, string[]? permissions) in systemRoles)
        {
            Role? existingRole = await context.Roles
                .FirstOrDefaultAsync(r => r.Name == name, cancellationToken);

            if (existingRole is not null)
            {
                if (logger is not null)
                {
                    LogRoleAlreadyExists(logger, name);
                }

                continue;
            }

            Result<Role> roleResult = Role.CreateSystemRole(name, displayName, description);
            if (roleResult.IsFailure)
            {
                if (logger is not null)
                {
                    LogRoleCreationFailed(logger, name, roleResult.Error.Description);
                }

                continue;
            }

            Role role = roleResult.Value;
            role.SetPermissions(permissions);

            context.Roles.Add(role);
            if (logger is not null)
            {
                LogRoleCreated(logger, name, permissions.Length);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
