namespace OpenIdentityStack.Application.Roles.Commands;

/// <summary>
/// Command to replace all permissions on a role.
/// </summary>
/// <param name="RoleId">The ID of the role.</param>
/// <param name="Permissions">The permissions to set, replacing any existing permissions.</param>
/// <param name="AcknowledgeWildcardGrant">Whether the caller acknowledged broad wildcard grants.</param>
public sealed record SetRolePermissionsCommand(
    RoleId RoleId,
    IReadOnlyList<string> Permissions,
    bool AcknowledgeWildcardGrant = false);
