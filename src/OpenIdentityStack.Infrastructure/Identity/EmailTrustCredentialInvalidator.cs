using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Persistence;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>Uses the same scoped EF context as the OpenIddict stores and trust transaction.</summary>
public sealed class EmailTrustCredentialInvalidator(
    OpenIdentityStackDbContext dbContext,
    IOpenIddictTokenManager tokens,
    IOpenIddictAuthorizationManager authorizations,
    IDateTimeProvider clock) : IEmailTrustCredentialInvalidator
{
    public async Task<EmailTrustCredentialInvalidation> RevokeAsync(UserId userId, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Credential invalidation requires the trust transaction.");
        }

        string subject = userId.Value.ToString();
        long revokedAuthorizations = await authorizations.RevokeBySubjectAsync(subject, cancellationToken);
        long revokedTokens = await tokens.RevokeBySubjectAsync(subject, cancellationToken);
        List<UserSession> sessions = await dbContext.UserSessions
            .Where(s => s.UserId == userId && s.Status == SessionStatus.Active).ToListAsync(cancellationToken);
        foreach (UserSession session in sessions)
        {
            session.Revoke(clock);
        }

        return new EmailTrustCredentialInvalidation(revokedTokens, revokedAuthorizations, sessions.Count);
    }
}
