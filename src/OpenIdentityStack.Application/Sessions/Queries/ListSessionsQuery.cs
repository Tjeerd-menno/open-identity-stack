using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Application.Sessions.Queries;

/// <summary>
/// Query to list sessions with pagination and optional filters.
/// </summary>
/// <param name="Page">The page number (1-based).</param>
/// <param name="PageSize">The number of items per page.</param>
/// <param name="UserId">Optional filter by user ID.</param>
/// <param name="Status">Optional filter by session status.</param>
/// <param name="Search">Optional case-insensitive IP address or user agent substring.</param>
public sealed record ListSessionsQuery(
    int Page = 1,
    int PageSize = 20,
    UserId? UserId = null,
    SessionStatus? Status = null,
    string? Search = null);

/// <summary>
/// Represents a single session in the list.
/// </summary>
/// <param name="Id">The session ID.</param>
/// <param name="UserId">The user ID.</param>
/// <param name="IpAddress">The IP address.</param>
/// <param name="UserAgent">The user agent.</param>
/// <param name="Status">The session status.</param>
/// <param name="ClientCount">Number of clients in this session.</param>
/// <param name="LastActivityAt">Last activity timestamp.</param>
/// <param name="ExpiresAt">When the session expires.</param>
/// <param name="CreatedAt">When the session was created.</param>
public sealed record SessionListItem(
    SessionId Id,
    UserId UserId,
    string IpAddress,
    string UserAgent,
    SessionStatus Status,
    int ClientCount,
    DateTimeOffset LastActivityAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);

/// <summary>
/// Result of listing sessions.
/// </summary>
/// <param name="Items">The list of sessions.</param>
/// <param name="TotalCount">The total number of sessions matching the query.</param>
/// <param name="Page">The current page number.</param>
/// <param name="PageSize">The page size.</param>
public sealed record ListSessionsResult(
    IReadOnlyList<SessionListItem> Items,
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
/// Interface for the list sessions query handler.
/// </summary>
public interface IListSessionsQueryHandler
{
    /// <summary>
    /// Executes the query to list sessions.
    /// </summary>
    Task<Result<ListSessionsResult>> ExecuteAsync(
        ListSessionsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Handler for listing sessions.
/// </summary>
public sealed class ListSessionsQueryHandler : IListSessionsQueryHandler
{
    private readonly ISessionRepository sessionRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListSessionsQueryHandler"/> class.
    /// </summary>
    public ListSessionsQueryHandler(ISessionRepository sessionRepository)
    {
        this.sessionRepository = sessionRepository;
    }

    /// <inheritdoc/>
    public async Task<Result<ListSessionsResult>> ExecuteAsync(
        ListSessionsQuery query,
        CancellationToken cancellationToken = default)
    {
        (IReadOnlyList<UserSession>? sessions, int totalCount) = await this.sessionRepository.ListAsync(
            query.Page,
            query.PageSize,
                query.UserId,
                query.Status,
                query.Search,
                cancellationToken);

            var items = sessions
            .Select(s => new SessionListItem(
                Id: s.Id,
                UserId: s.UserId,
                IpAddress: s.IpAddress,
                UserAgent: s.UserAgent,
                Status: s.Status,
                ClientCount: s.ClientSessions.Count,
                LastActivityAt: s.LastActivityAt,
                ExpiresAt: s.ExpiresAt,
                CreatedAt: s.CreatedAt))
            .ToList();

        return new ListSessionsResult(items, totalCount, query.Page, query.PageSize);
    }
}
