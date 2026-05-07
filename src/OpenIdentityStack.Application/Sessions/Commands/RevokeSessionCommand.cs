using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Application.Sessions.Commands;

/// <summary>
/// Command to revoke a user session.
/// </summary>
/// <param name="SessionId">The session ID to revoke.</param>
public sealed record RevokeSessionCommand(SessionId SessionId);

/// <summary>
/// Result of revoking a session.
/// </summary>
/// <param name="SessionId">The revoked session ID.</param>
/// <param name="RevokedAt">When the session was revoked.</param>
public sealed record RevokeSessionResult(SessionId SessionId, DateTimeOffset RevokedAt);

/// <summary>
/// Interface for the revoke session use case.
/// </summary>
public interface IRevokeSessionUseCase
{
    /// <summary>
    /// Revokes a session.
    /// </summary>
    Task<Result<RevokeSessionResult>> ExecuteAsync(
        RevokeSessionCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case for revoking a user session.
/// </summary>
public sealed class RevokeSessionUseCase : IRevokeSessionUseCase
{
    private readonly ISessionRepository sessionRepository;
    private readonly IDateTimeProvider dateTimeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RevokeSessionUseCase"/> class.
    /// </summary>
    public RevokeSessionUseCase(
        ISessionRepository sessionRepository,
        IDateTimeProvider dateTimeProvider)
    {
        this.sessionRepository = sessionRepository;
        this.dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc/>
    public async Task<Result<RevokeSessionResult>> ExecuteAsync(
        RevokeSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        UserSession? session = await this.sessionRepository.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return SessionErrors.NotFound;
        }

        Result revokeResult = session.Revoke(this.dateTimeProvider);
        if (revokeResult.IsFailure)
        {
            return revokeResult.Error;
        }

        await this.sessionRepository.UpdateAsync(session, cancellationToken);

        return new RevokeSessionResult(session.Id, session.RevokedAt!.Value);
    }
}
