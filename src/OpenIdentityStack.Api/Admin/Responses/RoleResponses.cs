using SharedKernel;
namespace OpenIdentityStack.Api.Admin.Responses;

/// <summary>
/// Response containing role details.
/// </summary>
public sealed record RoleResponse
{
    /// <summary>
    /// Gets or sets the role ID.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets or sets the normalized name of the role.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the role.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the role.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a system role.
    /// </summary>
    public bool IsSystemRole { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the role is active.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Gets or sets the permissions granted by this role.
    /// </summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];
}

/// <summary>
/// Response containing a paginated list of roles.
/// </summary>
public sealed record RolesListResponse
{
    /// <summary>
    /// Gets or sets the roles on the current page.
    /// </summary>
    public IReadOnlyList<RoleResponse> Items { get; init; } = [];

    /// <summary>
    /// Gets or sets the total count of roles.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets or sets the current page number.
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; init; }
}

/// <summary>
/// Response containing a user's assigned roles.
/// </summary>
public sealed record UserRolesResponse
{
    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Gets or sets the roles assigned to the user.
    /// </summary>
    public IReadOnlyList<RoleResponse> Roles { get; init; } = [];
}
