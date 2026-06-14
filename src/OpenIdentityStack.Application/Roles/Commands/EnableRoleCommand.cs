namespace OpenIdentityStack.Application.Roles.Commands;

/// <summary>
/// Command to enable a previously disabled role.
/// </summary>
/// <param name="RoleId">The ID of the role to enable.</param>
public sealed record EnableRoleCommand(RoleId RoleId);
