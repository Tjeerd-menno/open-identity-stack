namespace OpenIdentityStack.Application.Roles.Queries;

/// <summary>
/// Data transfer object for role information.
/// </summary>
/// <param name="Id">The role ID.</param>
/// <param name="Name">The role name.</param>
/// <param name="DisplayName">The user-friendly display name.</param>
/// <param name="Description">The role description.</param>
/// <param name="IsSystemRole">Whether this is a system role.</param>
/// <param name="IsActive">Whether the role is active.</param>
/// <param name="Permissions">The permissions granted by this role.</param>
public sealed record RoleDto(
    Guid Id,
    string Name,
    string DisplayName,
    string? Description,
    bool IsSystemRole,
    bool IsActive,
    IReadOnlyList<string> Permissions);
