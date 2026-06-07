using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Application.ApplicationPermissions.Validators;
using OpenIdentityStack.Domain.ApplicationPermissions;
using SharedKernel;

namespace OpenIdentityStack.Application.ApplicationPermissions.Planning;

public static class ApplicationPermissionChangePlanner
{
    public static Result<ApplicationPermissionChangePlan> CreateManifestPlan(
        RegisteredApplication application,
        PermissionManifestDocument manifest,
        string actorId,
        IDateTimeProvider dateTimeProvider)
    {
        Result validation = ApplicationPermissionManifestValidator.Validate(manifest, null, allowEmptyPermissions: true);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        var requestedKeys = manifest.Permissions.Select(permission => permission.Key).ToHashSet(StringComparer.Ordinal);
        var activePermissions = application.Permissions.Where(static permission => !permission.IsRemoved).ToList();
        var removals = activePermissions.Where(permission => !requestedKeys.Contains(permission.PermissionKey)).ToList();
        var permissionCreateResults = manifest.Permissions
            .Where(permission => activePermissions.All(existing => existing.PermissionKey != permission.Key))
            .Select(permission => ApplicationPermission.Create(
                application.Id,
                application.ApplicationIdentifier,
                permission.Key,
                permission.DisplayName,
                permission.Description,
                permission.Category,
                actorId,
                dateTimeProvider))
            .ToList();

        Result<ApplicationPermission>? firstFailure = permissionCreateResults.FirstOrDefault(result => result.IsFailure);
        if (firstFailure is not null && firstFailure.IsFailure)
        {
            return firstFailure.Error;
        }

        var additions = permissionCreateResults
            .Select(permissionResult => permissionResult.Value)
            .ToList();
        var metadataUpdates = manifest.Permissions
            .Select(permission => activePermissions.FirstOrDefault(existing => existing.PermissionKey == permission.Key))
            .Where(static permission => permission is not null)
            .Cast<ApplicationPermission>()
            .ToList();
        var remainingAfterRemoval = activePermissions.Except(removals).ToList();

        return new ApplicationPermissionChangePlan(
            application,
            additions,
            metadataUpdates,
            removals,
            CreateRemovalPlan(application, removals, remainingAfterRemoval),
            new PermissionAssignmentRemovalPlan(additions.Select(permission => permission.FullPermissionKey).ToList(), []));
    }

    public static ApplicationPermissionChangePlan CreatePermissionDeletionPlan(
        RegisteredApplication application,
        ApplicationPermission permission)
    {
        ApplicationPermission[] removals = [permission];
        var remaining = application.Permissions
            .Where(candidate => !candidate.IsRemoved && candidate.Id != permission.Id)
            .ToList();

        return new ApplicationPermissionChangePlan(
            application,
            [],
            [],
            removals,
            CreateRemovalPlan(application, removals, remaining),
            new PermissionAssignmentRemovalPlan([], []));
    }

    public static ApplicationPermissionChangePlan CreateApplicationDeletionPlan(RegisteredApplication application)
    {
        var removals = application.Permissions.Where(static permission => !permission.IsRemoved).ToList();
        var removalPlan = new PermissionAssignmentRemovalPlan(
            removals.Select(permission => permission.FullPermissionKey).ToList(),
            CreateApplicationWildcardPermissions(application, removals));

        return new ApplicationPermissionChangePlan(
            application,
            [],
            [],
            removals,
            removalPlan,
            new PermissionAssignmentRemovalPlan([], []));
    }

    private static PermissionAssignmentRemovalPlan CreateRemovalPlan(
        RegisteredApplication application,
        IReadOnlyList<ApplicationPermission> removals,
        IReadOnlyList<ApplicationPermission> remaining)
    {
        return new PermissionAssignmentRemovalPlan(
            removals.Select(permission => permission.FullPermissionKey).ToList(),
            CreateCollapsedWildcardPermissions(application, removals, remaining));
    }

    private static List<string> CreateCollapsedWildcardPermissions(
        RegisteredApplication application,
        IReadOnlyList<ApplicationPermission> removals,
        IReadOnlyList<ApplicationPermission> remaining)
    {
        return removals
            .Select(permission => permission.PermissionKey.Split(':', 2)[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(resource => !remaining.Any(permission => permission.PermissionKey.StartsWith($"{resource}:", StringComparison.OrdinalIgnoreCase)))
            .Select(resource => $"{application.ApplicationIdentifier}:{resource}:*")
            .ToList();
    }

    private static List<string> CreateApplicationWildcardPermissions(
        RegisteredApplication application,
        IReadOnlyList<ApplicationPermission> removals)
    {
        return removals
            .Select(permission => permission.PermissionKey.Split(':', 2)[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(resource => $"{application.ApplicationIdentifier}:{resource}:*")
            .ToList();
    }
}
