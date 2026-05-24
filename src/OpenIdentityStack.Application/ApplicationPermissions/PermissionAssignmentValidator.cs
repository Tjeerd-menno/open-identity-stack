using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.ApplicationPermissions;

public sealed class PermissionAssignmentValidator : IPermissionAssignmentValidator
{
    private static readonly HashSet<string> builtInPermissions = new(Permissions.GetAllPermissions(), StringComparer.OrdinalIgnoreCase)
    {
        Permissions.All,
        Permissions.Users.All,
        Permissions.Roles.All,
        Permissions.Groups.All,
        Permissions.Applications.All,
        Permissions.ApplicationPermissions.All,
        Permissions.Sessions.All,
        Permissions.Providers.All,
        Permissions.AuditLogs.All,
        Permissions.System.All
    };

    private readonly IApplicationPermissionRegistryRepository repository;

    public PermissionAssignmentValidator(IApplicationPermissionRegistryRepository repository)
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

            if (builtInPermissions.Contains(normalized))
            {
                continue;
            }

            if (normalized.EndsWith(":*", StringComparison.Ordinal))
            {
                string applicationIdentifier = normalized[..^2];
                RegisteredApplication? application = await this.repository.GetByIdentifierAsync(applicationIdentifier, cancellationToken).ConfigureAwait(false);
                if (application is { Status: ApplicationLifecycleStatus.Active })
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
