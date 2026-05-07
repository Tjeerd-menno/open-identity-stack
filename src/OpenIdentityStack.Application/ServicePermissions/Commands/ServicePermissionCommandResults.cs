namespace OpenIdentityStack.Application.ServicePermissions.Commands;

public sealed record RegisterServiceResult(
    Guid ServiceId,
    string ServiceIdentifier,
    int PermissionsRegistered);
