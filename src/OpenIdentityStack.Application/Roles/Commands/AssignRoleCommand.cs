using SharedKernel;
namespace OpenIdentityStack.Application.Roles.Commands;
/// <summary>
/// Command to assign a role to a user.
/// </summary>
/// <param name="UserId">The ID of the user.</param>
/// <param name="RoleId">The ID of the role to assign.</param>
/// <param name="ActorId">The ID of the admin performing the action (for auditing).</param>
public sealed record AssignRoleCommand(UserId UserId, RoleId RoleId, string ActorId);
