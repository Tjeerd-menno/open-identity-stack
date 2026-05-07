namespace OpenIdentityStack.Api.Common.Models;

/// <summary>
/// Request model for paginated queries.
/// </summary>
public record PagedRequest
{
    private const int defaultPageSize = 20;
    private const int maxPageSize = 100;
    private const int defaultPage = 1;

    private int page = defaultPage;
    private int pageSize = defaultPageSize;

    /// <summary>
    /// Gets or sets the page number (1-based).
    /// </summary>
    public int Page
    {
        get => this.page;
        set => this.page = value < 1 ? defaultPage : value;
    }

    /// <summary>
    /// Gets or sets the number of items per page.
    /// </summary>
    public int PageSize
    {
        get => this.pageSize;
        set => this.pageSize = value switch
        {
            < 1 => defaultPageSize,
            > maxPageSize => maxPageSize,
            _ => value
        };
    }

    /// <summary>
    /// Gets or sets the field to sort by.
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Gets or sets whether to sort in descending order.
    /// </summary>
    public bool SortDescending { get; set; }

    /// <summary>
    /// Gets the number of items to skip.
    /// </summary>
    public int Skip => (this.Page - 1) * this.PageSize;
}

/// <summary>
/// Response model for paginated results.
/// </summary>
/// <typeparam name="T">The type of items in the response.</typeparam>
public record PagedResponse<T>
{
    /// <summary>
    /// Gets the items for the current page.
    /// </summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>
    /// Gets the current page number (1-based).
    /// </summary>
    public required int Page { get; init; }

    /// <summary>
    /// Gets the number of items per page.
    /// </summary>
    public required int PageSize { get; init; }

    /// <summary>
    /// Gets the total number of items across all pages.
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)this.TotalCount / this.PageSize);

    /// <summary>
    /// Gets whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => this.Page > 1;

    /// <summary>
    /// Gets whether there is a next page.
    /// </summary>
    public bool HasNextPage => this.Page < this.TotalPages;
}

/// <summary>
/// Factory methods for creating paged responses.
/// </summary>
public static class PagedResponseFactory
{
    /// <summary>
    /// Creates a paged response from a list of items and pagination info.
    /// </summary>
    public static PagedResponse<T> Create<T>(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        return new PagedResponse<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    /// <summary>
    /// Creates an empty paged response.
    /// </summary>
    public static PagedResponse<T> Empty<T>(int page = 1, int pageSize = 20)
    {
        return new PagedResponse<T>
        {
            Items = [],
            Page = page,
            PageSize = pageSize,
            TotalCount = 0
        };
    }
}
