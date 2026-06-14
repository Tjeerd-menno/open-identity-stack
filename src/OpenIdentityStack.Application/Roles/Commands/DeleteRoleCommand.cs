namespace OpenIdentityStack.Application.Roles.Commands;

/// <summary>
/// Command to delete a role.
/// </summary>
/// <param name="RoleId">The ID of the role to delete.</param>
public sealed record DeleteRoleCommand(RoleId RoleId);
