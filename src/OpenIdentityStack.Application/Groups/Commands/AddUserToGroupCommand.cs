using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Commands;

/// <summary>
/// Command to add a user to a group.
/// </summary>
/// <param name="GroupId">The group ID.</param>
/// <param name="UserId">The user ID to add.</param>
/// <param name="AssignedBy">The user ID of the admin performing the action.</param>
public sealed record AddUserToGroupCommand(GroupId GroupId, UserId UserId, UserId AssignedBy);
