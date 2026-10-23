using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
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

    internal CredentialSessionValidator(
        ISessionRepository sessionRepository,
        IUserRepository userRepository,
        IDateTimeProvider dateTimeProvider)
    {
        this.sessionRepository = sessionRepository;
        this.userRepository = userRepository;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<User?> ValidateAsync(
        UserId userId,
        SessionId sessionId,
        CancellationToken cancellationToken = default)
    {
        SessionValidationSnapshot? session = await this.sessionRepository.GetValidationSnapshotAsync(sessionId, cancellationToken);
        if (session is null || !session.HasUsableSession(userId, this.dateTimeProvider.UtcNow))
        {
            return null;
        }

        User? user = await this.userRepository.GetReadOnlyByIdAsync(userId, cancellationToken);
        return user is not null && session.Accepts(userId, user.CanAuthenticate(), user.SecurityVersion, this.dateTimeProvider.UtcNow)
            ? user
            : null;
    }
}
