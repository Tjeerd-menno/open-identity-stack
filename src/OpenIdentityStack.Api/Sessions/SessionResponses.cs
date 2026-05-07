using SharedKernel;
namespace OpenIdentityStack.Api.Sessions;

/// <summary>
/// Response for a single session.
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
public sealed record SessionResponse(
    Guid Id,
    Guid UserId,
    string IpAddress,
    string UserAgent,
    string Status,
    int ClientCount,
    DateTimeOffset LastActivityAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);

/// <summary>
/// Paginated response for listing sessions.
/// </summary>
/// <param name="Items">The list of sessions.</param>
/// <param name="TotalCount">The total number of sessions.</param>
/// <param name="Page">The current page number.</param>
/// <param name="PageSize">The page size.</param>
/// <param name="TotalPages">The total number of pages.</param>
public sealed record SessionListResponse(
    IReadOnlyList<SessionResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>
/// Response for session revocation.
/// </summary>
/// <param name="SessionId">The revoked session ID.</param>
/// <param name="RevokedAt">When the session was revoked.</param>
public sealed record RevokeSessionResponse(
    Guid SessionId,
    DateTimeOffset RevokedAt);

/// <summary>
/// Response for revoking all user sessions.
/// </summary>
/// <param name="RevokedCount">The number of sessions revoked.</param>
/// <param name="RevokedAt">When the sessions were revoked.</param>
public sealed record RevokeAllSessionsResponse(
    int RevokedCount,
    DateTimeOffset RevokedAt);
