using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Queries;

/// <summary>
/// Query to list users with pagination.
/// </summary>
/// <param name="Page">The page number (1-based).</param>
/// <param name="PageSize">The number of items per page.</param>
/// <param name="Search">Optional search term for email or display name.</param>
public sealed record ListUsersQuery(int Page = 1, int PageSize = 20, string? Search = null);

/// <summary>
/// A single user item in the list.
/// </summary>
/// <param name="Id">The user ID.</param>
/// <param name="Email">The user's email.</param>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="Status">The user's status.</param>
/// <param name="CreatedAt">When the user was created.</param>
public sealed record UserListItem(
    UserId Id,
    string Email,
    string DisplayName,
    UserStatus Status,
    DateTimeOffset CreatedAt);

/// <summary>
/// Result of listing users.
/// </summary>
/// <param name="Items">The list of users.</param>
/// <param name="TotalCount">The total number of users matching the query.</param>
/// <param name="Page">The current page number.</param>
/// <param name="PageSize">The page size.</param>
public sealed record ListUsersResult(
    IReadOnlyList<UserListItem> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)this.TotalCount / this.PageSize);

    /// <summary>
    /// Gets whether there is a next page.
    /// </summary>
    public bool HasNextPage => this.Page < this.TotalPages;

    /// <summary>
    /// Gets whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => this.Page > 1;
}

/// <summary>
/// Interface for the list users query handler.
/// </summary>
public interface IListUsersQueryHandler
{
    /// <summary>
    /// Lists users with pagination.
    /// </summary>
    /// <param name="query">The list users query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the paginated list of users.</returns>
    Task<Result<ListUsersResult>> HandleAsync(
        ListUsersQuery query,
        CancellationToken cancellationToken = default);
}
