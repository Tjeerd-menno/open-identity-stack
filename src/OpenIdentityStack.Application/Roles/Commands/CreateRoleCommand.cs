namespace OpenIdentityStack.Application.Roles.Commands;
/// <summary>
/// Command to create a new role.
/// </summary>
/// <param name="Name">The name of the role (will be normalized to lowercase).</param>
/// <param name="DisplayName">The display name of the role. If null, uses the name.</param>
/// <param name="Description">Optional description of the role.</param>
/// <param name="Permissions">The permissions to assign to the role.</param>
public sealed record CreateRoleCommand(
    string Name,
    string? DisplayName,
    string? Description,
    IReadOnlyList<string>? Permissions);

/// <summary>
/// Response containing the created role details.
/// </summary>
/// <param name="Id">The ID of the created role.</param>
/// <param name="Name">The normalized name of the role.</param>
/// <param name="DisplayName">The display name of the role.</param>
/// <param name="Description">The description of the role.</param>
/// <param name="IsActive">Whether the role is active.</param>
public sealed record CreateRoleResponse(
    Guid Id,
    string Name,
    string DisplayName,
    string Description,
    bool IsActive);
