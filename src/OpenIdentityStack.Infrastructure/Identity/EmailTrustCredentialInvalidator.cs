using OpenIdentityStack.Application.Authorization;
using System.Text.Json;
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
        // Complete the reader before writes: PostgreSQL does not permit another command
        // while the subject-token enumeration owns the connection.
        var subjectTokens = new List<object>();
        await foreach (object token in tokens.FindBySubjectAsync(subject, cancellationToken)) { subjectTokens.Add(token); }
        long revokedTokens = 0;
        foreach (object token in subjectTokens)
        {
            System.Collections.Immutable.ImmutableDictionary<string, JsonElement> properties = await tokens.GetPropertiesAsync(token, cancellationToken);
            if (properties.TryGetValue(TokenSubjectClaims.Kind, out JsonElement kind)
                && kind.ValueKind == JsonValueKind.String && kind.GetString() == TokenSubjectClaims.Application)
            {
                continue;
            }
            if (await tokens.TryRevokeAsync(token, cancellationToken)) { revokedTokens++; }
        }
        List<UserSession> sessions = await dbContext.UserSessions
            .Where(s => s.UserId == userId && s.Status == SessionStatus.Active).ToListAsync(cancellationToken);
        foreach (UserSession session in sessions)
        {
            session.Revoke(clock);
        }

        return new EmailTrustCredentialInvalidation(revokedTokens, revokedAuthorizations, sessions.Count);
    }
}
