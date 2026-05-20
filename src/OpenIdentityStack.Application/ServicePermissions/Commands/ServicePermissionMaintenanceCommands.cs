using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Application.ServicePermissions.Commands;

public sealed record UpdateRegisteredServiceCommand(
    Guid ServiceId,
    string DisplayName,
    string? Description,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record AddServicePermissionCommand(
    Guid ServiceId,
    string PermissionKey,
    string DisplayName,
    string? Description,
    string? IntendedUse,
    string? DocumentationUrl,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record UpdateServicePermissionCommand(
    Guid PermissionId,
    string DisplayName,
    string? Description,
    string? IntendedUse,
    string? DocumentationUrl,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record ChangeRegisteredServiceLifecycleCommand(
    Guid ServiceId,
    ServiceLifecycleStatus Status,
    string ActorId,
    bool AcknowledgeDependencies,
    uint? ExpectedConcurrencyToken);

public sealed record ChangeServicePermissionLifecycleCommand(
    Guid PermissionId,
    PermissionLifecycleStatus Status,
    string ActorId,
    bool AcknowledgeDependencies,
    uint? ExpectedConcurrencyToken);

public sealed record TransferRegisteredServiceOwnershipCommand(
    Guid ServiceId,
    string OwnerId,
    OwnerType OwnerType,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record AddDelegatedMaintainerCommand(
    Guid ServiceId,
    string PrincipalId,
    OwnerType PrincipalType,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record RemoveDelegatedMaintainerCommand(
    Guid ServiceId,
    string PrincipalId,
    string ActorId,
    uint? ExpectedConcurrencyToken);
