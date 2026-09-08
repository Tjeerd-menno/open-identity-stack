using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Validates that a credential-bearing request still belongs to an active session.
/// </summary>
public interface ICredentialSessionValidator
{
    Task<bool> IsValidAsync(
        UserId userId,
        SessionId sessionId,
        CancellationToken cancellationToken = default);
}
