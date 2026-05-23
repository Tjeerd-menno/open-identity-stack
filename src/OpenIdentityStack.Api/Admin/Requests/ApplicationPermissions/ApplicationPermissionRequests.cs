namespace OpenIdentityStack.Api.Admin.Requests.ApplicationPermissions;

public sealed record PermissionManifestApplicationRequest(
    string Id,
    string Name,
    string? Version);

public sealed record PermissionManifestEntryRequest(
    string Name,
    string Description,
    string? Category);

public sealed record PermissionManifestRequest(
    PermissionManifestApplicationRequest Application,
    IReadOnlyList<PermissionManifestEntryRequest> Permissions);

public sealed record ImportPermissionManifestRequest(string Endpoint);

public sealed record UpdateRegisteredApplicationRequest(
    string DisplayName,
    string? Description,
    uint? ConcurrencyToken);

public sealed record AddApplicationPermissionRequest(
    string PermissionKey,
    string DisplayName,
    string? Description,
    string? IntendedUse,
    string? DocumentationUrl,
    uint? ConcurrencyToken);

public sealed record UpdateApplicationPermissionRequest(
    string DisplayName,
    string? Description,
    string? IntendedUse,
    string? DocumentationUrl,
    uint? ConcurrencyToken);

public sealed record ChangeApplicationLifecycleRequest(
    string Status,
    bool AcknowledgeDependencies,
    uint? ConcurrencyToken);

public sealed record ChangePermissionLifecycleRequest(
    string Status,
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
