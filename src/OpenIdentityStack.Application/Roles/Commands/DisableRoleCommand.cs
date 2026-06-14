namespace OpenIdentityStack.Application.Roles.Commands;

/// <summary>
/// Command to disable a role.
/// </summary>
/// <param name="RoleId">The ID of the role to disable.</param>
public sealed record DisableRoleCommand(RoleId RoleId);
