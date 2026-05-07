namespace OpenIdentityStack.Application.Roles.Queries;

/// <summary>
/// Query to list roles with pagination.
/// </summary>
/// <param name="Page">The page number (1-based).</param>
/// <param name="PageSize">The number of items per page.</param>
/// <param name="IncludeInactive">Whether to include inactive roles.</param>
/// <param name="Search">Optional search term to filter roles by name or display name.</param>
public sealed record ListRolesQuery(
    int Page = 1,
    int PageSize = 20,
    bool IncludeInactive = false,
    string? Search = null);

/// <summary>
/// Response containing a paginated list of roles.
/// </summary>
/// <param name="Items">The roles on the current page.</param>
/// <param name="TotalCount">The total count of roles.</param>
/// <param name="Page">The current page number.</param>
/// <param name="PageSize">The page size.</param>
public sealed record ListRolesResponse(
    IReadOnlyList<RoleDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

