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
