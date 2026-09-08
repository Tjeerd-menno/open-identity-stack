using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Sessions;

/// <summary>
/// Reads authoritative persisted session and user state before accepting a credential.
/// </summary>
public sealed class CredentialSessionValidator : ICredentialSessionValidator
{
    private readonly ISessionRepository sessionRepository;
    private readonly IUserRepository userRepository;
    private readonly IDateTimeProvider dateTimeProvider;

    public CredentialSessionValidator(
        ISessionRepository sessionRepository,
        IUserRepository userRepository,
        IDateTimeProvider dateTimeProvider)
    {
        this.sessionRepository = sessionRepository;
        this.userRepository = userRepository;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<bool> IsValidAsync(
        UserId userId,
        SessionId sessionId,
        CancellationToken cancellationToken = default)
    {
        UserSession? session = await this.sessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session is null || session.UserId != userId || session.Status != SessionStatus.Active || session.IsExpired(this.dateTimeProvider))
        {
            return false;
        }

        User? user = await this.userRepository.GetByIdAsync(userId, cancellationToken);
        return user is not null && user.CanAuthenticate() && user.SecurityVersion == session.UserSecurityVersion;
    }
}
