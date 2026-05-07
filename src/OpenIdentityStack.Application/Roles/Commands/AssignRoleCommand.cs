using SharedKernel;
namespace OpenIdentityStack.Application.Roles.Commands;
/// <summary>
/// Command to assign a role to a user.
/// </summary>
/// <param name="UserId">The ID of the user.</param>
/// <param name="RoleId">The ID of the role to assign.</param>
public sealed record AssignRoleCommand(UserId UserId, RoleId RoleId);
