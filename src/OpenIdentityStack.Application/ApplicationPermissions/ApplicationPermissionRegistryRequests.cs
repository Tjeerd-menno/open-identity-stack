using OpenIdentityStack.Application.ApplicationPermissions.Commands;
using OpenIdentityStack.Application.ApplicationPermissions.Validators;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.ApplicationPermissions;

public sealed record RegisterManualApplicationRequest(
    string ApplicationIdentifier,
    string DisplayName,
    string? Description,
    string OwnerId,
    OwnerType OwnerType,
    string ActorId,
    IReadOnlyList<RegisterApplicationPermissionInput> Permissions);

public sealed record ImportManifestRequest(
    PermissionManifestDocument Manifest,
    string OwnerId,
    OwnerType OwnerType,
    string? ManifestBaseUrl,
    string ActorId,
    bool IsImported = false);

public sealed record PreviewManifestRequest(
    Guid ApplicationId,
    PermissionManifestDocument? Manifest,
    string ActorId,
    uint? ExpectedConcurrencyToken,
    bool FetchRemote = false);

public sealed record ApplyManifestRequest(
    Guid ApplicationId,
    PermissionManifestDocument? Manifest,
    string ActorId,
    uint? ExpectedConcurrencyToken,
    bool FetchRemote = false);

public sealed record UpdateApplicationRequest(
    Guid ApplicationId,
    string DisplayName,
    string? Description,
    string ActorId,
    uint? ExpectedConcurrencyToken,
    string? ManifestBaseUrl = null);

public sealed record AddPermissionRequest(
    Guid ApplicationId,
    string PermissionKey,
    string DisplayName,
    string? Description,
    string? Category,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record UpdatePermissionRequest(
    Guid PermissionId,
    string DisplayName,
    string? Description,
    string? Category,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record ChangeLifecycleRequest(
    Guid ApplicationId,
    ApplicationLifecycleStatus Status,
    string ActorId,
    bool AcknowledgeDependencies,
    uint? ExpectedConcurrencyToken);

public sealed record TransferOwnershipRequest(
    Guid ApplicationId,
    string OwnerId,
    OwnerType OwnerType,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record AddMaintainerRequest(
    Guid ApplicationId,
    string PrincipalId,
    OwnerType PrincipalType,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record RemoveMaintainerRequest(
    Guid ApplicationId,
    string PrincipalId,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record PreviewDeletePermissionRequest(
    Guid PermissionId,
    string ActorId);

public sealed record DeletePermissionRequest(
    Guid PermissionId,
    string Reason,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record PreviewDeleteApplicationRequest(
    Guid ApplicationId,
    string ActorId);

public sealed record DeleteApplicationRequest(
    Guid ApplicationId,
    string Reason,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record UpdateRemovedPermissionReplacementRequest(
    Guid PermissionId,
    string ReplacementFullPermissionKey,
    string ReplacementNote,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record ListHistoryRequest(
    string? ApplicationIdentifier,
    bool IncludeApplications,
    bool IncludePermissions,
    string ActorId);

public sealed record ListDiagnosticsRequest(string ActorId);
