using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Application.Sessions.Queries;

/// <summary>
/// Query to validate if a session is still active.
/// </summary>
/// <param name="SessionId">The session ID to validate.</param>
/// <param name="Credential">Owner facts already loaded by the caller. When set, the session is loaded once and those facts are checked before a status reason is returned.</param>
public sealed record ValidateSessionQuery(SessionId SessionId, CredentialSessionCheck? Credential = null);

/// <summary>
/// User facts already read by the caller so session validation does not load the user again.
/// </summary>
public sealed record CredentialSessionCheck(UserId UserId, long SecurityVersion, bool CanAuthenticate);

/// <summary>
/// Result of session validation.
/// </summary>
/// <param name="IsValid">Whether the session is valid.</param>
/// <param name="Reason">Reason if session is invalid.</param>
public sealed record ValidateSessionResult(bool IsValid, string? Reason = null)
{
    /// <summary>
    /// Gets whether the credential owner check failed. Callers show the generic credential error instead of <see cref="Reason"/>.
    /// </summary>
    public bool CredentialRejected { get; init; }
}

/// <summary>
/// Query handler interface for validating sessions.
/// </summary>
public interface IValidateSessionQueryHandler
{
    /// <summary>
    /// Validates whether a session is still active and can be used.
    /// </summary>
    /// <param name="query">The validation query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    Task<ValidateSessionResult> HandleAsync(
        ValidateSessionQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Query handler for validating sessions.
/// </summary>
public sealed class ValidateSessionQueryHandler : IValidateSessionQueryHandler
{
    private readonly ISessionRepository sessionRepository;
    private readonly IDateTimeProvider dateTimeProvider;

    internal ValidateSessionQueryHandler(
        ISessionRepository sessionRepository,
        IDateTimeProvider dateTimeProvider)
    {
        this.sessionRepository = sessionRepository;
        this.dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<ValidateSessionResult> HandleAsync(
        ValidateSessionQuery query,
        CancellationToken cancellationToken = default)
    {
        UserSession? session = await this.sessionRepository.GetByIdAsync(query.SessionId, cancellationToken);

        if (session is null)
        {
            return query.Credential is null
                ? new ValidateSessionResult(false, "Session not found")
                : new ValidateSessionResult(false) { CredentialRejected = true };
        }

        if (query.Credential is { } credential)
        {
            var snapshot = new SessionValidationSnapshot(
                session.UserId,
                session.Status,
                session.ExpiresAt,
                session.UserSecurityVersion);
            if (!snapshot.Accepts(credential.UserId, credential.CanAuthenticate, credential.SecurityVersion, this.dateTimeProvider.UtcNow))
            {
                return new ValidateSessionResult(false) { CredentialRejected = true };
            }
        }

        if (session.Status == SessionStatus.Revoked)
        {
            return new ValidateSessionResult(false, "Session has been revoked");
        }

        if (session.Status == SessionStatus.LoggedOut)
        {
            return new ValidateSessionResult(false, "Session has been logged out");
        }

        if (session.Status == SessionStatus.Expired || session.IsExpired(this.dateTimeProvider))
        {
            return new ValidateSessionResult(false, "Session has expired");
        }

        if (session.Status != SessionStatus.Active)
        {
            return new ValidateSessionResult(false, "Session is not active");
        }

        // Session is valid, update last activity
        session.UpdateLastActivity(this.dateTimeProvider);
        await this.sessionRepository.UpdateAsync(session, cancellationToken);

        return new ValidateSessionResult(true);
    }
}
