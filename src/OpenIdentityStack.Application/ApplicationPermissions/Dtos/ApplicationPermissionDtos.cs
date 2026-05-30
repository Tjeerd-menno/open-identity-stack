namespace OpenIdentityStack.Application.ApplicationPermissions.Dtos;

public sealed record RegisteredApplicationDto(
    Guid Id,
    string ApplicationIdentifier,
    string DisplayName,
    string? Description,
    string OwnerId,
    string OwnerType,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    uint ConcurrencyToken,
    IReadOnlyList<ApplicationPermissionDto> Permissions,
    IReadOnlyList<DelegatedMaintainerDto> Maintainers,
    string SchemaVersion = "1.0.0",
    string ManifestVersion = "0.0.0",
    string? ManifestBaseUrl = null);

public sealed record ApplicationPermissionDto(
    Guid Id,
    string PermissionKey,
    string FullPermissionKey,
    string DisplayName,
    string? Description,
    string? Category,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ApplicationId = null,
    string? ApplicationName = null,
    string? ApplicationVersion = null,
    string Kind = "concrete",
    bool Assignable = true,
    string? ResourceOrAggregate = null,
    int? CoveredPermissionCount = null,
    DateTimeOffset? RemovedAt = null,
    string? RemovedBy = null,
    string? RemoveReason = null,
    string? ReplacementFullPermissionKey = null,
    string? ReplacementNote = null,
    uint? ConcurrencyToken = null);

public sealed record RemovedPermissionDetailDto(
    Guid Id,
    Guid ApplicationId,
    string ApplicationIdentifier,
    string PermissionKey,
    string FullPermissionKey,
    string DisplayName,
    string? Description,
    string? Category,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset RemovedAt,
    string RemovedBy,
    string RemoveReason,
    string? ReplacementFullPermissionKey,
    string? ReplacementNote,
    uint ConcurrencyToken);

public sealed record DeletedApplicationHistoryDto(
    Guid Id,
    string ApplicationIdentifier,
    string DisplayName,
    string OwnerId,
    string OwnerType,
    string Status,
    int PermissionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset DeletedAt,
    string DeletedBy,
    string DeleteReason,
    uint ConcurrencyToken);

public sealed record ApplicationPermissionHistoryDto(
    IReadOnlyList<DeletedApplicationHistoryDto> DeletedApplications,
    IReadOnlyList<RemovedPermissionDetailDto> RemovedPermissions);

public sealed record PermissionDiagnosticIssueDto(
    string Kind,
    string AssignmentStore,
    string AssignmentId,
    string? AssignmentDisplayName,
    string Permission,
    string Message);

public sealed record PermissionDiagnosticsDto(IReadOnlyList<PermissionDiagnosticIssueDto> Issues);

public sealed record PlatformPermissionCatalogItemDto(
    string Permission,
    string Resource,
    string Action,
    string Kind,
    string DisplayName,
    bool Assignable);

public sealed record PermissionAssignmentImpactDto(
    string AssignmentStore,
    string AssignmentId,
    string AssignmentDisplayName,
    string Permission,
    string ImpactKind);

public sealed record PermissionAssignmentRemovalPlan(
    IReadOnlyList<string> ExactPermissions,
    IReadOnlyList<string> WildcardPermissionsToRemove);

public sealed record DestructiveOperationResultDto(
    IReadOnlyList<ApplicationPermissionDto> RemovedPermissions,
    IReadOnlyList<PermissionAssignmentImpactDto> ExactAssignmentsRemoved,
    IReadOnlyList<PermissionAssignmentImpactDto> WildcardAssignmentsRemoved,
    IReadOnlyList<PermissionAssignmentImpactDto> WildcardAssignmentsImpacted,
    bool MetadataUpdated,
    bool ManifestVersionAdvanced,
    string? AuditId = null);

public sealed record DeletionImpactDto(
    Guid TargetId,
    string TargetType,
    IReadOnlyList<PermissionAssignmentImpactDto> AssignmentImpacts,
    IReadOnlyList<string> Warnings);

public sealed record ManifestPreviewDto(
    Guid ApplicationId,
    string CurrentManifestVersion,
    string RequestedManifestVersion,
    bool HasChanges,
    bool VersionWillAdvance,
    bool IsDestructive,
    IReadOnlyList<ApplicationPermissionDto> Additions,
    IReadOnlyList<ApplicationPermissionDto> MetadataUpdates,
    IReadOnlyList<ApplicationPermissionDto> Removals,
    IReadOnlyList<PermissionAssignmentImpactDto> AssignmentImpacts);

public sealed record ManifestApplyDto(
    RegisteredApplicationDto Application,
    DestructiveOperationResultDto Result);

public sealed record DelegatedMaintainerDto(
    Guid Id,
    string PrincipalId,
    string PrincipalType,
    string GrantedBy,
    DateTimeOffset GrantedAt);

public sealed record RegisteredApplicationSummaryDto(
    Guid Id,
    string ApplicationIdentifier,
    string DisplayName,
    string OwnerId,
    string Status,
    int PermissionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
