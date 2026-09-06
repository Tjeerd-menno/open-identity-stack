using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Validation;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Persistence;

namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>Rejects stale user credentials, including issuance racing a committed trust withdrawal.</summary>
public sealed class UserCredentialRevisionValidation(OpenIdentityStackDbContext dbContext) :
    IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenContext>,
    IOpenIddictValidationHandler<OpenIddictValidationEvents.ValidateTokenContext>
{
    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenContext context)
    {
        // Logout hints and client assertions do not authorize user access or refresh grants.
        if (context.Principal?.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.IdentityToken) == true ||
            context.Principal?.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.ClientAssertion) == true) { return; }
        if (!await this.IsCurrentAsync(context.Principal, context.CancellationToken))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken, "The credential is no longer valid.");
        }
    }

    public async ValueTask HandleAsync(OpenIddictValidationEvents.ValidateTokenContext context)
    {
        if (!await this.IsCurrentAsync(context.Principal, context.CancellationToken))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken, "The credential is no longer valid.");
        }
    }

    private async Task<bool> IsCurrentAsync(ClaimsPrincipal? principal, CancellationToken cancellationToken)
    {
        if (principal is null)
        {
            return true;
        }

        string? capturedRevision = principal.GetClaim(UserCredentialClaims.Revision);
        Claim[] subjectKinds = principal.FindAll(TokenSubjectClaims.Kind).ToArray();
        if (subjectKinds is [{ Value: TokenSubjectClaims.Application }]
            && capturedRevision is null
            && principal.GetClaim(OpenIddictConstants.Claims.Subject) is { Length: > 0 } applicationSubject
            && string.Equals(applicationSubject, principal.GetClaim(OpenIddictConstants.Claims.ClientId), StringComparison.Ordinal))
        {
            return true;
        }

        if (!Guid.TryParse(principal.GetClaim(OpenIddictConstants.Claims.Subject), out Guid subject))
        {
            return capturedRevision is null;
        }

        // A fresh scalar query avoids both identity-map and OpenIddict token-cache state.
        Guid? currentRevision = await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == new UserId(subject)).Select(u => (Guid?)u.CredentialRevision)
            .SingleOrDefaultAsync(cancellationToken);
        if (!currentRevision.HasValue)
        {
            return capturedRevision is null; // Application subjects have no user revision.
        }

        // Legacy credentials are only eligible while no withdrawal has invalidated this user.
        return capturedRevision is null
            ? currentRevision == Guid.Empty
            : Guid.TryParse(capturedRevision, out Guid revision) && revision == currentRevision;
    }
}
