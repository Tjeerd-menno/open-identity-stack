using OpenIdentityStack.Application.Abstractions;

using SharedKernel;
namespace OpenIdentityStack.Application.Sessions.Commands;

/// <summary>
/// Command to revoke all sessions for a user.
/// </summary>
/// <param name="UserId">The user ID whose sessions should be revoked.</param>
/// <param name="ExcludeSessionId">Optional session ID to exclude from revocation (current session).</param>
public sealed record RevokeAllUserSessionsCommand(
    UserId UserId,
    SessionId? ExcludeSessionId,
    string ActorId);

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
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly ICredentialTerminationService terminationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RevokeAllUserSessionsUseCase"/> class.
    /// </summary>
    public RevokeAllUserSessionsUseCase(
        IDateTimeProvider dateTimeProvider,
        ICredentialTerminationService terminationService)
    {
        this.dateTimeProvider = dateTimeProvider;
        this.terminationService = terminationService;
    }

    /// <inheritdoc/>
    public async Task<Result<RevokeAllUserSessionsResult>> ExecuteAsync(
        RevokeAllUserSessionsCommand command,
        CancellationToken cancellationToken = default)
    {
        Result<int> revokeResult = await this.terminationService.TerminateAllSessionsAsync(
            command.UserId,
            command.ActorId,
            "administrative-revoke-all",
            command.ExcludeSessionId,
            cancellationToken);
        if (revokeResult.IsFailure)
        {
            return revokeResult.Error;
        }

        return new RevokeAllUserSessionsResult(revokeResult.Value, this.dateTimeProvider.UtcNow);
    }
}
