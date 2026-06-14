namespace OpenIdentityStack.Application.Roles.Commands;

/// <summary>
/// Command to remove a single permission from a role.
/// </summary>
/// <param name="RoleId">The ID of the role.</param>
/// <param name="Permission">The permission to remove.</param>
public sealed record RemoveRolePermissionCommand(RoleId RoleId, string Permission);
