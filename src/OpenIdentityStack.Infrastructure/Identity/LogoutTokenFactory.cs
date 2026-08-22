using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIdentityStack.Domain.Common;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>
/// Creates logout tokens signed with the OpenIddict server's asymmetric signing key, as
/// required by OpenID Connect Back-Channel Logout 1.0 section 2.4. Relying parties verify
/// the signature against the keys published on the JWKS endpoint.
/// </summary>
public sealed class LogoutTokenFactory : ILogoutTokenFactory
{
    /// <summary>
    /// The <c>typ</c> header value that distinguishes a logout token from an ID token,
    /// preventing the substitution attack described in the specification.
    /// </summary>
    private const string LogoutTokenType = "logout+jwt";

    /// <summary>
    /// The back-channel logout event identifier that must appear in the <c>events</c> claim.
    /// </summary>
    private const string BackChannelLogoutEvent = "http://schemas.openid.net/event/backchannel-logout";

    /// <summary>
    /// Logout tokens are consumed immediately by the relying party, so a short lifetime
    /// bounds the replay window without risking clock-skew failures.
    /// </summary>
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(2);

    private readonly IOptionsMonitor<OpenIddictServerOptions> serverOptions;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IDateTimeProvider dateTimeProvider;

    public LogoutTokenFactory(
        IOptionsMonitor<OpenIddictServerOptions> serverOptions,
        IHttpContextAccessor httpContextAccessor,
        IDateTimeProvider dateTimeProvider)
    {
        this.serverOptions = serverOptions;
        this.httpContextAccessor = httpContextAccessor;
        this.dateTimeProvider = dateTimeProvider;
    }

    public string CreateLogoutToken(SessionId sessionId, string clientId)
    {
        OpenIddictServerOptions options = this.serverOptions.CurrentValue;

        SigningCredentials credentials = SelectSigningCredentials(options);
        string issuer = this.ResolveIssuer(options);

        DateTimeOffset issuedAt = this.dateTimeProvider.UtcNow;

        // The session identifier travels in 'sid'. 'sub' is deliberately omitted: the
        // specification requires 'sub', 'sid', or both, and this notifier has no access to
        // the end user's subject identifier. Emitting the session id as 'sub' — as an earlier
        // revision did — makes relying parties resolve a principal that does not exist.
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = clientId,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = issuedAt.Add(TokenLifetime).UtcDateTime,
            TokenType = LogoutTokenType,
            SigningCredentials = credentials,
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [OpenIddictConstants.Claims.JwtId] = Guid.NewGuid().ToString(),
                [OpenIddictConstants.Claims.SessionId] = sessionId.Value.ToString(),
                ["events"] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    [BackChannelLogoutEvent] = new Dictionary<string, object>(StringComparer.Ordinal),
                },
            },
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>
    /// Selects an asymmetric signing key. Symmetric keys are rejected because a relying party
    /// cannot verify them from the JWKS endpoint, and unsigned tokens are never produced.
    /// </summary>
    private static SigningCredentials SelectSigningCredentials(OpenIddictServerOptions options)
    {
        return options.SigningCredentials.FirstOrDefault(credentials => credentials.Key is AsymmetricSecurityKey)
            ?? throw new InvalidOperationException(
                "No asymmetric signing credentials are registered. A back-channel logout token cannot be "
                + "signed, and an unsigned token would be rejected by conforming relying parties.");
    }

    /// <summary>
    /// Resolves the issuer the same way OpenIddict does: the configured issuer when present,
    /// otherwise the base URI of the request being handled.
    /// </summary>
    private string ResolveIssuer(OpenIddictServerOptions options)
    {
        if (options.Issuer is not null)
        {
            return options.Issuer.AbsoluteUri.TrimEnd('/');
        }

        HttpRequest? request = this.httpContextAccessor.HttpContext?.Request;
        if (request is not null && request.Host.HasValue)
        {
            return new UriBuilder(request.Scheme, request.Host.Host)
            {
                Port = request.Host.Port ?? -1,
                Path = request.PathBase.ToString(),
            }.Uri.AbsoluteUri.TrimEnd('/');
        }

        throw new InvalidOperationException(
            "The issuer could not be resolved. Configure 'OpenIddict:Issuer' so back-channel logout "
            + "tokens carry an issuer relying parties can validate.");
    }
}
