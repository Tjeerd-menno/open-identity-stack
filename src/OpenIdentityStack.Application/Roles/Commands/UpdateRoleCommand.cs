namespace OpenIdentityStack.Application.Roles.Commands;

/// <summary>
/// Command to update a role's display name, description, and (optionally) permissions.
/// </summary>
/// <param name="RoleId">The ID of the role to update.</param>
/// <param name="DisplayName">The new display name. If null, the display name is left unchanged.</param>
/// <param name="Description">The new description.</param>
/// <param name="Permissions">The permissions to set. When null, existing permissions are left unchanged.</param>
/// <param name="AcknowledgeWildcardGrant">Whether the caller acknowledged broad wildcard grants.</param>
public sealed record UpdateRoleCommand(
    RoleId RoleId,
    string? DisplayName,
    string? Description,
    IReadOnlyList<string>? Permissions,
    bool AcknowledgeWildcardGrant = false);
