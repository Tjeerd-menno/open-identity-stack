using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.ApplicationPermissions.Commands;

public sealed record UpdateRegisteredApplicationCommand(
    Guid ApplicationId,
    string DisplayName,
    string? Description,
    string ActorId,
    uint? ExpectedConcurrencyToken,
    string? ManifestBaseUrl = null);

public sealed record AddApplicationPermissionCommand(
    Guid ApplicationId,
    string PermissionKey,
    string DisplayName,
    string? Description,
    string? Category,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record UpdateApplicationPermissionCommand(
    Guid PermissionId,
    string DisplayName,
    string? Description,
    string? Category,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record DeleteApplicationPermissionCommand(
    Guid PermissionId,
    string Reason,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record PreviewDeleteApplicationPermissionCommand(
    Guid PermissionId,
    string ActorId);

public sealed record DeleteRegisteredApplicationCommand(
    Guid ApplicationId,
    string Reason,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record PreviewDeleteRegisteredApplicationCommand(
    Guid ApplicationId,
    string ActorId);

public sealed record ChangeRegisteredApplicationLifecycleCommand(
    Guid ApplicationId,
    ApplicationLifecycleStatus Status,
    string ActorId,
    bool AcknowledgeDependencies,
    uint? ExpectedConcurrencyToken);

public sealed record TransferRegisteredApplicationOwnershipCommand(
    Guid ApplicationId,
    string OwnerId,
    OwnerType OwnerType,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record AddDelegatedMaintainerCommand(
    Guid ApplicationId,
    string PrincipalId,
    OwnerType PrincipalType,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record RemoveDelegatedMaintainerCommand(
    Guid ApplicationId,
    string PrincipalId,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record UpdateRemovedPermissionReplacementCommand(
    Guid PermissionId,
    string ReplacementFullPermissionKey,
    string ReplacementNote,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record ListApplicationPermissionHistoryQuery(
    string? ApplicationIdentifier,
    bool IncludeApplications,
    bool IncludePermissions,
    string ActorId);

public sealed record ListApplicationPermissionDiagnosticsQuery(string ActorId);
