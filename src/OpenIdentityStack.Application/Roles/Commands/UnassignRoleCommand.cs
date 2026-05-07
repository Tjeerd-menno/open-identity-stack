using SharedKernel;
namespace OpenIdentityStack.Application.Roles.Commands;
/// <summary>
/// Command to unassign a role from a user.
/// </summary>
/// <param name="UserId">The ID of the user.</param>
/// <param name="RoleId">The ID of the role to unassign.</param>
public sealed record UnassignRoleCommand(UserId UserId, RoleId RoleId);
