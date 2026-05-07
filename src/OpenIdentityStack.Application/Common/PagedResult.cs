using SharedKernel;
namespace OpenIdentityStack.Application.Common;

#pragma warning disable CA1000 // Do not declare static members on generic types

/// <summary>
/// Represents a paginated result set from queries.
/// </summary>
/// <typeparam name="T">The type of items in the result.</typeparam>
public sealed record PagedResult<T>
{
    /// <summary>
    /// The items on this page.
    /// </summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>
    /// The current page number (1-based).
    /// </summary>
    public required int Page { get; init; }

    /// <summary>
    /// The number of items per page.
    /// </summary>
    public required int PageSize { get; init; }

    /// <summary>
    /// The total number of items across all pages.
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// The total number of pages.
    /// </summary>
    public int TotalPages => this.PageSize > 0 ? (int)Math.Ceiling(this.TotalCount / (double)this.PageSize) : 0;

    /// <summary>
    /// Whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => this.Page > 1;

    /// <summary>
    /// Whether there is a next page.
    /// </summary>
    public bool HasNextPage => this.Page < this.TotalPages;

    /// <summary>
    /// Creates an empty result.
    /// </summary>
    public static PagedResult<T> Empty(int page = 1, int pageSize = 20)
    {
        return new PagedResult<T>
        {
            Items = [],
            Page = page,
            PageSize = pageSize,
            TotalCount = 0
        };
    }

    /// <summary>
    /// Creates a result from items and pagination info.
    /// </summary>
    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        return new PagedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
