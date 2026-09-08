using System.Security.Claims;

using OpenIddict.Validation;
using OpenIddict.Server;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using SharedKernel;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenIdentityStack.Api.Authentication;

/// <summary>
/// Rejects locally validated user access tokens when their authoritative session is no longer usable.
/// Client credentials tokens carry an issuer-signed token kind marker and have no user session.
/// </summary>
public sealed class AuthoritativeCredentialValidationHandler
    : IOpenIddictValidationHandler<OpenIddictValidationEvents.ProcessAuthenticationContext>
{
    private readonly ICredentialSessionValidator credentialSessionValidator;
    private readonly CurrentAuthorizationClaimsProjector authorizationClaimsProjector;

    public AuthoritativeCredentialValidationHandler(
        ICredentialSessionValidator credentialSessionValidator,
        CurrentAuthorizationClaimsProjector authorizationClaimsProjector)
    {
        this.credentialSessionValidator = credentialSessionValidator;
        this.authorizationClaimsProjector = authorizationClaimsProjector;
    }

    public async ValueTask HandleAsync(OpenIddictValidationEvents.ProcessAuthenticationContext context)
    {
        ClaimsPrincipal? principal = context.AccessTokenPrincipal;
        if (principal is null || principal.HasClaim("token_kind", "client_credentials"))
        {
            return;
        }

        string? subject = principal.FindFirstValue(Claims.Subject)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        string? session = principal.FindFirstValue("sid")
            ?? principal.FindFirstValue(TokenClaimProjectionService.LegacySessionIdClaim);

        if (!Guid.TryParse(subject, out Guid userId)
            || !Guid.TryParse(session, out Guid sessionId)
            || !await this.credentialSessionValidator.IsValidAsync(
                new UserId(userId), new SessionId(sessionId)))
        {
            context.Reject(
                error: Errors.InvalidToken,
                description: "The access token is no longer valid.");
            return;
        }

        await this.authorizationClaimsProjector.RefreshAsync(principal, new UserId(userId));
    }
}

/// <summary>
/// Makes an introspected user token inactive when its persisted session or user epoch changed.
/// </summary>
public sealed class AuthoritativeIntrospectionHandler
    : IOpenIddictServerHandler<OpenIddictServerEvents.HandleIntrospectionRequestContext>
{
    private readonly ICredentialSessionValidator credentialSessionValidator;

    public AuthoritativeIntrospectionHandler(ICredentialSessionValidator credentialSessionValidator)
    {
        this.credentialSessionValidator = credentialSessionValidator;
    }

    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleIntrospectionRequestContext context)
    {
        ClaimsPrincipal principal = context.GenericTokenPrincipal;
        if (principal.HasClaim("token_kind", "client_credentials"))
        {
            return;
        }

        string? subject = principal.FindFirstValue(Claims.Subject) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        string? session = principal.FindFirstValue("sid") ?? principal.FindFirstValue(TokenClaimProjectionService.LegacySessionIdClaim);
        if (!Guid.TryParse(subject, out Guid userId)
            || !Guid.TryParse(session, out Guid sessionId)
            || !await this.credentialSessionValidator.IsValidAsync(new UserId(userId), new SessionId(sessionId)))
        {
            context.Reject(
                error: Errors.InvalidToken,
                description: "The access token is no longer valid.");
        }
    }
}
