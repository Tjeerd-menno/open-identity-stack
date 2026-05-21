namespace OpenIdentityStack.Api.Admin.Requests.ServicePermissions;

public sealed record RegisterServicePermissionRequest(
    string PermissionKey,
    string DisplayName,
    string? Description,
    string? IntendedUse,
    string? DocumentationUrl);

public sealed record RegisterServiceRequest(
    string ServiceIdentifier,
    string DisplayName,
    string? Description,
    string OwnerId,
    string OwnerType,
    IReadOnlyList<RegisterServicePermissionRequest> Permissions);

public sealed record UpdateRegisteredServiceRequest(
    string DisplayName,
    string? Description,
    uint? ConcurrencyToken);

public sealed record AddServicePermissionRequest(
    string PermissionKey,
    string DisplayName,
    string? Description,
    string? IntendedUse,
    string? DocumentationUrl,
    uint? ConcurrencyToken);

public sealed record UpdateServicePermissionRequest(
    string DisplayName,
    string? Description,
    string? IntendedUse,
    string? DocumentationUrl,
    uint? ConcurrencyToken);

public sealed record ChangeServiceLifecycleRequest(
    string Status,
    bool AcknowledgeDependencies,
    uint? ConcurrencyToken);

public sealed record ChangePermissionLifecycleRequest(
    string Status,
    bool AcknowledgeDependencies,
    uint? ConcurrencyToken);

public sealed record TransferServiceOwnershipRequest(
    string OwnerId,
    string OwnerType,
    uint? ConcurrencyToken);

public sealed record AddDelegatedMaintainerRequest(
    string PrincipalId,
    string PrincipalType,
    uint? ConcurrencyToken);
