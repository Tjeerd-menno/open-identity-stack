using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Validates that a credential-bearing request still belongs to an active session.
/// </summary>
public interface ICredentialSessionValidator
{
    /// <summary>
    /// Returns the user when the session is still a usable credential for that user.
    /// </summary>
    Task<User?> ValidateAsync(
        UserId userId,
        SessionId sessionId,
        CancellationToken cancellationToken = default);
}
