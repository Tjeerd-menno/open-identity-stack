using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Repository interface for session persistence operations.
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    /// Gets a session by its ID.
    /// </summary>
    Task<UserSession?> GetByIdAsync(SessionId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all sessions for a user.
    /// </summary>
    Task<IReadOnlyList<UserSession>> GetByUserIdAsync(
        UserId userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active sessions for a user.
    /// </summary>
    Task<IReadOnlyList<UserSession>> GetActiveByUserIdAsync(
        UserId userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new session.
    /// </summary>
    Task AddAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing session.
    /// </summary>
    Task UpdateAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists sessions with pagination.
    /// </summary>
    Task<(IReadOnlyList<UserSession> Sessions, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        UserId? userIdFilter = null,
        SessionStatus? statusFilter = null,
        string? search = null,
        CancellationToken cancellationToken = default);
}
