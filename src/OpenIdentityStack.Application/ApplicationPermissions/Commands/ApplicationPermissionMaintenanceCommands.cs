using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.ApplicationPermissions.Commands;

public sealed record UpdateRegisteredApplicationCommand(
    Guid ApplicationId,
    string DisplayName,
    string? Description,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record AddApplicationPermissionCommand(
    Guid ApplicationId,
    string PermissionKey,
    string DisplayName,
    string? Description,
    string? IntendedUse,
    string? DocumentationUrl,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record UpdateApplicationPermissionCommand(
    Guid PermissionId,
    string DisplayName,
    string? Description,
    string? IntendedUse,
    string? DocumentationUrl,
    string ActorId,
    uint? ExpectedConcurrencyToken);

public sealed record ChangeRegisteredApplicationLifecycleCommand(
    Guid ApplicationId,
    ApplicationLifecycleStatus Status,
    string ActorId,
    bool AcknowledgeDependencies,
    uint? ExpectedConcurrencyToken);

public sealed record ChangeApplicationPermissionLifecycleCommand(
    Guid PermissionId,
    PermissionLifecycleStatus Status,
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
