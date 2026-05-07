using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Commands;

/// <summary>
/// Command to remove a user from a group.
/// </summary>
/// <param name="GroupId">The group ID.</param>
/// <param name="UserId">The user ID to remove.</param>
public sealed record RemoveUserFromGroupCommand(GroupId GroupId, UserId UserId);
