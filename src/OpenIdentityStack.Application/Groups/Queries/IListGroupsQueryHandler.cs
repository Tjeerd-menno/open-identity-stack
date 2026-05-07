using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Queries;

/// <summary>
/// Handler interface for listing groups.
/// </summary>
public interface IListGroupsQueryHandler
{
    /// <summary>
    /// Lists groups with pagination and optional search.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="search">Optional search term.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the group list response.</returns>
    Task<Result<GroupListResponse>> HandleAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        CancellationToken cancellationToken = default);
}
