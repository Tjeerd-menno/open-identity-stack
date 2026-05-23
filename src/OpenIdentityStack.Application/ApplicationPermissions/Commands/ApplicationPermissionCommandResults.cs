namespace OpenIdentityStack.Application.ApplicationPermissions.Commands;

public sealed record RegisterApplicationResult(
    Guid ApplicationId,
    string ApplicationIdentifier,
    int PermissionsRegistered);
