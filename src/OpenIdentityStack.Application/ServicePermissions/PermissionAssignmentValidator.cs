using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Application.ServicePermissions;

public sealed class PermissionAssignmentValidator : IPermissionAssignmentValidator
{
    private static readonly HashSet<string> BuiltInPermissions = new(Permissions.GetAllPermissions(), StringComparer.OrdinalIgnoreCase)
    {
        Permissions.All,
        Permissions.Users.All,
        Permissions.Roles.All,
        Permissions.Groups.All,
        Permissions.ServiceAccounts.All,
        Permissions.ServicePermissions.All,
        Permissions.Sessions.All,
        Permissions.Providers.All,
        Permissions.Clients.All,
        Permissions.AuditLogs.All,
        Permissions.System.All
    };

    private readonly IServicePermissionRegistryRepository repository;

    public PermissionAssignmentValidator(IServicePermissionRegistryRepository repository)
    {
        this.repository = repository;
    }

    public async Task<Result> ValidateAssignableAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default)
    {
        foreach (string permission in permissions)
        {
            string normalized = permission.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return DomainError.Validation("PermissionAssignment.PermissionRequired", "Permission is required.");
            }

            if (BuiltInPermissions.Contains(normalized))
            {
                continue;
            }

            if (normalized.EndsWith(":*", StringComparison.Ordinal))
            {
                string serviceIdentifier = normalized[..^2];
                RegisteredService? service = await this.repository.GetByIdentifierAsync(serviceIdentifier, cancellationToken).ConfigureAwait(false);
                if (service is { Status: ServiceLifecycleStatus.Active })
                {
                    continue;
                }

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
}
