namespace OpenIdentityStack.Application.ServicePermissions.Commands;

public sealed record RegisterServicePermissionInput(
    string PermissionKey,
    string DisplayName,
    string? Description = null,
    string? IntendedUse = null,
    string? DocumentationUrl = null);

public sealed record RegisterServiceCommand(
    string ServiceIdentifier,
    string DisplayName,
    string? Description,
    string OwnerId,
    string OwnerType,
    string ActorId,
    IReadOnlyList<RegisterServicePermissionInput> Permissions);
