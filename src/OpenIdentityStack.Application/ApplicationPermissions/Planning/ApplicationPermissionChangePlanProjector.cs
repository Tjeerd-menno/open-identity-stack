using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.ApplicationPermissions.Planning;

public static class ApplicationPermissionChangePlanProjector
{
    public static ManifestPreviewDto ToManifestPreview(
        ApplicationPermissionChangePlan plan,
        string requestedManifestVersion,
        IReadOnlyList<PermissionAssignmentImpactDto> assignmentImpacts)
    {
        return new ManifestPreviewDto(
            plan.Application.Id.Value,
            plan.Application.ManifestVersion,
            requestedManifestVersion,
            plan.HasChanges,
            true,
            plan.IsDestructive,
            plan.Additions.Select(permission => ToPermissionDto(plan.Application, permission)).ToList(),
            plan.MetadataUpdates.Select(permission => ToPermissionDto(plan.Application, permission)).ToList(),
            plan.Removals.Select(permission => ToPermissionDto(plan.Application, permission)).ToList(),
            assignmentImpacts);
    }

    public static DestructiveOperationResultDto ToDestructiveResult(
        RegisteredApplication application,
        IReadOnlyList<ApplicationPermission> removedPermissions,
        IReadOnlyList<PermissionAssignmentImpactDto> assignmentImpacts,
        bool metadataUpdated,
        bool manifestVersionAdvanced)
    {
        return new DestructiveOperationResultDto(
            removedPermissions.Select(permission => ToPermissionDto(application, permission)).ToList(),
            assignmentImpacts.Where(static impact => impact.ImpactKind == AssignmentImpactKinds.ExactRemoved).ToList(),
            assignmentImpacts.Where(static impact => impact.ImpactKind == AssignmentImpactKinds.WildcardRemoved).ToList(),
            assignmentImpacts.Where(static impact => impact.ImpactKind == AssignmentImpactKinds.WildcardImpacted).ToList(),
            metadataUpdated,
            manifestVersionAdvanced);
    }

    public static RegisteredApplicationDto ToApplicationDto(RegisteredApplication application)
    {
        var permissions = application.Permissions.Where(static p => !p.IsRemoved).Select(p => new ApplicationPermissionDto(
            p.Id.Value,
            p.PermissionKey,
            p.FullPermissionKey,
            p.DisplayName,
            p.Description,
            p.Category,
            p.CreatedAt,
            p.ModifiedAt,
            application.ApplicationIdentifier,
            application.DisplayName,
            application.ManifestVersion)).ToList();

        var maintainers = application.Maintainers.Select(m => new DelegatedMaintainerDto(
            m.Id.Value,
            m.PrincipalId,
            m.PrincipalType.ToString(),
            m.GrantedBy,
            m.GrantedAt)).ToList();

        return new RegisteredApplicationDto(
            application.Id.Value,
            application.ApplicationIdentifier,
            application.DisplayName,
            application.Description,
            application.OwnerId,
            application.OwnerType.ToString(),
            application.Status.ToString(),
            application.CreatedAt,
            application.ModifiedAt,
            application.ConcurrencyToken,
            permissions,
            maintainers,
            application.SchemaVersion,
            application.ManifestVersion,
            application.ManifestBaseUrl);
    }

    public static ApplicationPermissionDto ToPermissionDto(RegisteredApplication application, ApplicationPermission permission)
    {
        return new ApplicationPermissionDto(
            permission.Id.Value,
            permission.PermissionKey,
            permission.FullPermissionKey,
            permission.DisplayName,
            permission.Description,
            permission.Category,
            permission.CreatedAt,
            permission.ModifiedAt,
            application.ApplicationIdentifier,
            application.DisplayName,
            application.ManifestVersion,
            RemovedAt: permission.RemovedAt,
            RemovedBy: permission.RemovedBy,
            RemoveReason: permission.RemoveReason,
            ReplacementFullPermissionKey: permission.ReplacementFullPermissionKey,
            ReplacementNote: permission.ReplacementNote,
            ConcurrencyToken: application.ConcurrencyToken);
    }
}
