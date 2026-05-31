using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.ApplicationPermissions;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Infrastructure.Persistence;

namespace OpenIdentityStack.Infrastructure.ApplicationPermissions;

public sealed partial class PermissionDiagnosticsReader : IPermissionDiagnosticsReader
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

    private readonly OpenIdentityStackDbContext dbContext;

    public PermissionDiagnosticsReader(OpenIdentityStackDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<PermissionDiagnosticsDto> ListIssuesAsync(CancellationToken cancellationToken = default)
    {
        List<Role> roles = await this.dbContext.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        List<RegisteredApplication> applications = await this.dbContext.RegisteredApplications
            .AsNoTracking()
            .Include(application => application.Permissions)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var issues = new List<PermissionDiagnosticIssueDto>();
        foreach (Role role in roles)
        {
            foreach (string permission in role.Permissions)
            {
                PermissionDiagnosticIssueDto? issue = Classify(role, permission, applications);
                if (issue is not null)
                {
                    issues.Add(issue);
                }
            }
        }

        return new PermissionDiagnosticsDto(issues);
    }

    private static PermissionDiagnosticIssueDto? Classify(Role role, string permission, IReadOnlyList<RegisteredApplication> applications)
    {
        if (builtInPermissions.Contains(permission))
        {
            return null;
        }

        string[] parts = permission.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is not (3 or 4) || !PermissionSegmentRegex().IsMatch(permission.Replace(":*", ":wildcard", StringComparison.Ordinal)))
        {
            return Issue("malformed", role, permission, "Permission string is malformed.");
        }

        RegisteredApplication? application = applications.FirstOrDefault(candidate =>
            string.Equals(candidate.ApplicationIdentifier, parts[0], StringComparison.OrdinalIgnoreCase));
        if (application is null)
        {
            return Issue("unknown", role, permission, "Permission references an unknown application.");
        }

        if (application.IsDeleted)
        {
            return Issue("deletedApplication", role, permission, "Permission references a deleted application.");
        }

        if (parts.Length == 3 && parts[2] == "*")
        {
            bool hasCurrentResourcePermission = application.Permissions.Any(candidate =>
                !candidate.IsRemoved
                && candidate.PermissionKey.StartsWith($"{parts[1]}:", StringComparison.OrdinalIgnoreCase));
            return hasCurrentResourcePermission
                ? null
                : Issue("collapsedWildcard", role, permission, "Wildcard assignment no longer covers any current permissions.");
        }

        if (parts.Length == 3)
        {
            string localKey = $"{parts[1]}:{parts[2]}";
            ApplicationPermission? exactPermission = application.Permissions.FirstOrDefault(candidate =>
                string.Equals(candidate.PermissionKey, localKey, StringComparison.OrdinalIgnoreCase));
            if (exactPermission is null)
            {
                return Issue("orphaned", role, permission, "Permission references no current or historical application permission.");
            }

            return exactPermission.IsRemoved
                ? Issue("tombstonedPermission", role, permission, "Permission references a removed application permission.")
                : null;
        }

        return Issue("malformed", role, permission, "Permission wildcard syntax is malformed.");
    }

    private static PermissionDiagnosticIssueDto Issue(string kind, Role role, string permission, string message)
    {
        return new PermissionDiagnosticIssueDto(kind, "role", role.Id.Value.ToString(), role.DisplayName, permission, message);
    }

    [GeneratedRegex(@"^[a-z][a-z0-9-]{1,62}:[a-z][a-z0-9-]{1,62}:[a-z][a-z0-9-]{1,62}(?::[a-z][a-z0-9-]{1,62}|:wildcard)?$")]
    private static partial Regex PermissionSegmentRegex();
}
