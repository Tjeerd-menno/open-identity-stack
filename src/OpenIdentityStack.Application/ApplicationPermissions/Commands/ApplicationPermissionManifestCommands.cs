using OpenIdentityStack.Application.ApplicationPermissions.Validators;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.ApplicationPermissions.Commands;

public sealed record CreateApplicationPermissionManifestCommand(
    PermissionManifestDocument Manifest,
    string OwnerId,
    OwnerType OwnerType,
    string? ManifestBaseUrl,
    string ActorId,
    bool IsImported = false);

public sealed record ApplyApplicationPermissionManifestCommand(
    Guid ApplicationId,
    PermissionManifestDocument Manifest,
    string ActorId,
    uint? ExpectedConcurrencyToken,
    bool AcknowledgeRedeclare = false,
    bool AcknowledgeWildcardImpact = false);

public sealed record RemoteApplicationPermissionManifestCommand(
    Guid ApplicationId,
    string ActorId,
    uint? ExpectedConcurrencyToken,
    bool AcknowledgeRedeclare = false,
    bool AcknowledgeWildcardImpact = false);
