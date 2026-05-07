namespace OpenIdentityStack.Application.Groups.Commands;

/// <summary>
/// Command to create a new group.
/// </summary>
/// <param name="Name">The name of the group.</param>
/// <param name="Description">Optional description of the group.</param>
public sealed record CreateGroupCommand(string Name, string? Description);

/// <summary>
/// Response containing the created group details.
/// </summary>
/// <param name="Id">The ID of the created group.</param>
/// <param name="Name">The name of the group.</param>
/// <param name="Description">The description of the group.</param>
public sealed record CreateGroupResponse(Guid Id, string Name, string? Description);
