namespace OpenIdentityStack.Application.ApplicationPermissions.Commands;

public sealed record RegisterApplicationPermissionInput(
    string PermissionKey,
    string DisplayName,
    string? Description = null,
    string? Category = null);

public sealed record RegisterApplicationCommand(
    string ApplicationIdentifier,
    string DisplayName,
    string? Description,
    string OwnerId,
    string OwnerType,
    string ActorId,
    IReadOnlyList<RegisterApplicationPermissionInput> Permissions);

public sealed record PermissionManifestApplication(
    string Id,
    string Name,
    string? Version = null);

public sealed record PermissionManifestEntry(
    string Name,
    string Description,
    string? Category = null);

public sealed record RegisterPermissionManifestCommand(
    PermissionManifestApplication Application,
    string ActorId,
    IReadOnlyList<PermissionManifestEntry> Permissions);
