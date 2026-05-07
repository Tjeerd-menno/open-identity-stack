
using System.ComponentModel.DataAnnotations;

namespace OpenIdentityStack.Api.Admin.Requests;
/// <summary>
/// Request to create a new role.
/// </summary>
public sealed record CreateRoleRequest
{
    /// <summary>
    /// Gets or sets the name of the role (will be normalized to lowercase).
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the display name of the role. If not provided, the name is used.
    /// </summary>
    [StringLength(100)]
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets or sets the description of the role.
    /// </summary>
    [StringLength(500)]
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the permissions to assign to the role.
    /// </summary>
    public IReadOnlyList<string>? Permissions { get; init; }
}

/// <summary>
/// Request to update a role.
/// </summary>
public sealed record UpdateRoleRequest
{
    /// <summary>
    /// Gets or sets the description of the role.
    /// </summary>
    [StringLength(500)]
    public string? Description { get; init; }
}

/// <summary>
/// Request to set permissions on a role.
/// </summary>
public sealed record SetRolePermissionsRequest
{
    /// <summary>
    /// Gets or sets the permissions to set on the role.
    /// This replaces all existing permissions.
    /// </summary>
    [Required]
    public required IReadOnlyList<string> Permissions { get; init; }
}

/// <summary>
/// Request to add a single permission to a role.
/// </summary>
public sealed record AddPermissionRequest
{
    /// <summary>
    /// Gets or sets the permission to add.
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string Permission { get; init; }
}

/// <summary>
/// Request to remove a permission from a role.
/// </summary>
public sealed record RemovePermissionRequest
{
    /// <summary>
    /// Gets or sets the permission to remove.
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string Permission { get; init; }
}
