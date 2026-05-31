namespace OpenIdentityStack.Api.Admin.Requests.ApplicationPermissions;

public sealed record PermissionManifestApplicationRequest(
    string Id,
    string DisplayName,
    string? Description,
    string Version);

public sealed record PermissionManifestEntryRequest(
    string Key,
    string DisplayName,
    string? Description,
    string? Category);

public sealed record PermissionManifestRequest(
    string SchemaVersion,
    PermissionManifestApplicationRequest Application,
    IReadOnlyList<PermissionManifestEntryRequest> Permissions);

public sealed record CreatePermissionManifestRequest(
    PermissionManifestRequest Manifest,
    string OwnerId,
    string OwnerType,
    string? ManifestBaseUrl);

public sealed record ManifestUpdateRequest(
    PermissionManifestRequest Manifest,
    uint? ConcurrencyToken);

public sealed record ImportPermissionManifestRequest(string Endpoint);

public sealed record RemoteImportRequest(uint? ConcurrencyToken);

public sealed record UpdateRegisteredApplicationRequest(
    string DisplayName,
    string? Description,
    uint? ConcurrencyToken,
    string? ManifestBaseUrl = null);

public sealed record AddApplicationPermissionRequest(
    string PermissionKey,
    string DisplayName,
    string? Description,
    string? Category,
    uint? ConcurrencyToken);

public sealed record UpdateApplicationPermissionRequest(
    string DisplayName,
    string? Description,
    string? Category,
    uint? ConcurrencyToken);

public sealed record DeleteApplicationPermissionRequest(
    string Reason,
    uint? ConcurrencyToken);

public sealed record DeleteRegisteredApplicationRequest(
    string Reason,
    uint? ConcurrencyToken);

public sealed record ReplacementGuidanceRequest(
    string ReplacementFullPermissionKey,
    string ReplacementNote,
    uint? ConcurrencyToken);

public sealed record ChangeApplicationLifecycleRequest(
    string Status,
    bool AcknowledgeDependencies,
    uint? ConcurrencyToken);

public sealed record ApplicationLifecycleActionRequest(
    bool AcknowledgeDependencies,
    uint? ConcurrencyToken);

public sealed record TransferApplicationOwnershipRequest(
    string OwnerId,
    string OwnerType,
    uint? ConcurrencyToken);

public sealed record AddDelegatedMaintainerRequest(
    string PrincipalId,
    string PrincipalType,
    uint? ConcurrencyToken);
