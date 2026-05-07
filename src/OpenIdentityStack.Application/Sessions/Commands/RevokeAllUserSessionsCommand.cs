using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Sessions.Commands;

/// <summary>
/// Command to revoke all sessions for a user.
/// </summary>
/// <param name="UserId">The user ID whose sessions should be revoked.</param>
/// <param name="ExcludeSessionId">Optional session ID to exclude from revocation (current session).</param>
public sealed record RevokeAllUserSessionsCommand(
    UserId UserId,
    SessionId? ExcludeSessionId = null);

/// <summary>
/// Result of revoking all user sessions.
/// </summary>
/// <param name="RevokedCount">The number of sessions revoked.</param>
/// <param name="RevokedAt">When the sessions were revoked.</param>
public sealed record RevokeAllUserSessionsResult(int RevokedCount, DateTimeOffset RevokedAt);

/// <summary>
/// Interface for the revoke all user sessions use case.
/// </summary>
public interface IRevokeAllUserSessionsUseCase
{
    /// <summary>
    /// Revokes all sessions for a user.
    /// </summary>
    Task<Result<RevokeAllUserSessionsResult>> ExecuteAsync(
        RevokeAllUserSessionsCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case for revoking all sessions for a user.
/// </summary>
public sealed class RevokeAllUserSessionsUseCase : IRevokeAllUserSessionsUseCase
{
    private readonly ISessionRepository sessionRepository;
    private readonly IUserRepository userRepository;
    private readonly IDateTimeProvider dateTimeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RevokeAllUserSessionsUseCase"/> class.
    /// </summary>
    public RevokeAllUserSessionsUseCase(
        ISessionRepository sessionRepository,
        IUserRepository userRepository,
        IDateTimeProvider dateTimeProvider)
    {
        this.sessionRepository = sessionRepository;
        this.userRepository = userRepository;
        this.dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc/>
    public async Task<Result<RevokeAllUserSessionsResult>> ExecuteAsync(
        RevokeAllUserSessionsCommand command,
        CancellationToken cancellationToken = default)
    {
        // Verify user exists
        User? user = await this.userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        // Get all active sessions for the user
        IReadOnlyList<UserSession> sessions = await this.sessionRepository.GetActiveByUserIdAsync(command.UserId, cancellationToken);

        int revokedCount = 0;
        foreach (UserSession session in sessions)
        {
            // Skip the excluded session (e.g., current session)
            if (command.ExcludeSessionId.HasValue && session.Id == command.ExcludeSessionId.Value)
            {
                continue;
            }

            Result revokeResult = session.Revoke(this.dateTimeProvider);
            if (revokeResult.IsSuccess)
            {
                await this.sessionRepository.UpdateAsync(session, cancellationToken);
                revokedCount++;
            }
        }

        return new RevokeAllUserSessionsResult(revokedCount, this.dateTimeProvider.UtcNow);
    }
}
