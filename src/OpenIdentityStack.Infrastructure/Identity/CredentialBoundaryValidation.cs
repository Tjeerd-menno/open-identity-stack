using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Validation;
using OpenIdentityStack.Application.Abstractions;

namespace OpenIdentityStack.Infrastructure.Identity;

public sealed class CredentialBoundaryValidation(ICredentialBoundaryStore boundary) :
    IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenContext>,
    IOpenIddictValidationHandler<OpenIddictValidationEvents.ValidateTokenContext>,
    IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenContext context)
    {
        // Client assertions prove client authentication and are not OP-issued grants.
        if (context.Principal is not null && !context.Principal.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.ClientAssertion)
            && !context.Principal.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.IdentityToken)
            && !await boundary.IsCurrentAsync(context.Principal.GetClaim(CredentialBoundaryClaims.Epoch), context.CancellationToken))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken, "The credential is no longer valid.");
        }
    }

    public async ValueTask HandleAsync(OpenIddictValidationEvents.ValidateTokenContext context)
    {
        if (context.Principal is not null && !await boundary.IsCurrentAsync(context.Principal.GetClaim(CredentialBoundaryClaims.Epoch), context.CancellationToken))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken, "The credential is no longer valid.");
        }
    }

    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (context.Principal is null) { context.Reject(OpenIddictConstants.Errors.InvalidGrant); return; }
        if (context.Request.IsClientCredentialsGrantType())
        {
            context.Principal.SetClaim(CredentialBoundaryClaims.Epoch, (await boundary.GetEpochAsync(context.CancellationToken)).ToString());
            foreach (System.Security.Claims.Claim claim in context.Principal.FindAll(CredentialBoundaryClaims.Epoch)) { claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken); }
        }
        else if (!await boundary.IsCurrentAsync(context.Principal.GetClaim(CredentialBoundaryClaims.Epoch), context.CancellationToken))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidGrant, "Fresh authentication is required.");
        }
    }
}
