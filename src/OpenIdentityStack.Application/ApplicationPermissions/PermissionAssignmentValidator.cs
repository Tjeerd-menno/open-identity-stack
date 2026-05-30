using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.ApplicationPermissions;

public sealed class PermissionAssignmentValidator : IPermissionAssignmentValidator
{
    private const string broadGrantAcknowledgementRequired = "RolePermissions.BroadGrantAcknowledgementRequired";

    private static readonly HashSet<string> builtInPermissions = new(Permissions.GetAllPermissions(), StringComparer.OrdinalIgnoreCase)
    {
        Permissions.All,
        Permissions.Users.All,
        Permissions.Roles.All,
        Permissions.Groups.All,
        Permissions.ServiceAccounts.All,
        Permissions.ApplicationPermissions.All,
        Permissions.Sessions.All,
        Permissions.Providers.All,
        Permissions.Clients.All,
        Permissions.AuditLogs.All,
        Permissions.System.All
    };

    private static readonly HashSet<string> builtInBroadGrants = new(StringComparer.OrdinalIgnoreCase)
    {
        Permissions.All,
        Permissions.Users.All,
        Permissions.Roles.All,
        Permissions.Groups.All,
        Permissions.ServiceAccounts.All,
        Permissions.ApplicationPermissions.All,
        Permissions.Sessions.All,
        Permissions.Providers.All,
        Permissions.Clients.All,
        Permissions.AuditLogs.All,
        Permissions.System.All
    };

    private readonly IApplicationPermissionRegistryRepository repository;

    public PermissionAssignmentValidator(IApplicationPermissionRegistryRepository repository)
    {
        this.repository = repository;
    }

    public async Task<Result> ValidateAssignableAsync(
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default)
    {
        return await this.ValidateAssignableAsync(permissions, false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> ValidateAssignableAsync(
        IEnumerable<string> permissions,
        bool acknowledgeWildcardGrant,
        CancellationToken cancellationToken = default)
    {
        foreach (string permission in permissions)
        {
            string normalized = permission.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return DomainError.Validation("PermissionAssignment.PermissionRequired", "Permission is required.");
            }

            if (builtInPermissions.Contains(normalized))
            {
                if (builtInBroadGrants.Contains(normalized) && !acknowledgeWildcardGrant)
                {
                    return DomainError.Conflict(
                        broadGrantAcknowledgementRequired,
                        $"Permission '{normalized}' is a broad grant and requires acknowledgement.");
                }

                continue;
            }

            string[] parts = normalized.Split(':');
            if (parts.Length == 3 && parts[2] == "*")
            {
                if (!acknowledgeWildcardGrant)
                {
                    return DomainError.Conflict(
                        broadGrantAcknowledgementRequired,
                        $"Permission '{normalized}' is a broad grant and requires acknowledgement.");
                }

                string applicationIdentifier = parts[0];
                string resourceOrAggregate = parts[1];
                RegisteredApplication? application = await this.repository.GetByIdentifierAsync(applicationIdentifier, cancellationToken).ConfigureAwait(false);
                if (application is { Status: ApplicationLifecycleStatus.Active } &&
                    application.Permissions.Any(p => GetResourceOrAggregate(p.PermissionKey) == resourceOrAggregate))
                {
                    continue;
                }

                return DomainError.Validation("PermissionAssignment.PermissionUnavailable", $"Permission '{normalized}' is not assignable.");
            }

            if (parts.Length != 3 || parts.Any(string.IsNullOrWhiteSpace) || parts.Any(part => part == "*"))
            {
                return DomainError.Validation("PermissionAssignment.PermissionUnavailable", $"Permission '{normalized}' is not assignable.");
            }

            if (await this.repository.IsPermissionAssignableAsync(normalized, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            return DomainError.Validation("PermissionAssignment.PermissionUnavailable", $"Permission '{normalized}' is not assignable.");
        }

        return Result.Success();
    }

    private static string GetResourceOrAggregate(string permissionKey)
    {
        int separatorIndex = permissionKey.IndexOf(':', StringComparison.Ordinal);
        return separatorIndex < 0 ? permissionKey : permissionKey[..separatorIndex];
    }
}
