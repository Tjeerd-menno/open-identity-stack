using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Application.Abstractions;

/// <summary>Invalidates credentials within the caller's trust-withdrawal transaction.</summary>
public interface IEmailTrustCredentialInvalidator
{
    Task<EmailTrustCredentialInvalidation> RevokeAsync(UserId userId, CancellationToken cancellationToken);
}

public sealed record EmailTrustCredentialInvalidation(long Tokens, long Authorizations, int Sessions);
