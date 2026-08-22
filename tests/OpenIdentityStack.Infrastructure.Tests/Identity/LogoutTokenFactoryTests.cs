using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Infrastructure.Identity;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Tests.Identity;

/// <summary>
/// Unit tests for <see cref="LogoutTokenFactory"/>.
///
/// These assert the properties OpenID Connect Back-Channel Logout 1.0 section 2.4 requires of a
/// logout token. The signature assertions exist because an earlier revision emitted an
/// <c>alg: none</c> token, which conforming relying parties reject and lenient ones accept from
/// anyone who can reach their endpoint.
/// </summary>
public sealed class LogoutTokenFactoryTests : IDisposable
{
    private const string BackChannelLogoutEvent = "http://schemas.openid.net/event/backchannel-logout";
    private const string ConfiguredIssuer = "https://identity.example.com";

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    public LogoutTokenFactoryTests()
    {
        this._dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
    }

    public void Dispose() => this._rsa.Dispose();

    [Fact]
    public void CreateLogoutToken_SignsTokenWithAsymmetricKey()
    {
        LogoutTokenFactory factory = this.CreateFactory();

        string token = factory.CreateLogoutToken(SessionId.Create(), "client-1");

        JsonWebToken parsed = new JsonWebTokenHandler().ReadJsonWebToken(token);
        parsed.Alg.ShouldBe(SecurityAlgorithms.RsaSha256);
        parsed.Alg.ShouldNotBe("none");
    }

    [Fact]
    public void CreateLogoutToken_ProducesTokenThatValidatesAgainstThePublicKey()
    {
        LogoutTokenFactory factory = this.CreateFactory();

        string token = factory.CreateLogoutToken(SessionId.Create(), "client-1");

        // A relying party fetches the public key from the JWKS endpoint and validates with it.
        TokenValidationResult result = new JsonWebTokenHandler().ValidateTokenAsync(
            token,
            new TokenValidationParameters
            {
                ValidIssuer = ConfiguredIssuer,
                ValidAudience = "client-1",
                IssuerSigningKey = new RsaSecurityKey(this._rsa.ExportParameters(false)),
                ValidateIssuerSigningKey = true,
                ValidateLifetime = false,
            }).GetAwaiter().GetResult();

        result.Exception.ShouldBeNull();
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void CreateLogoutToken_RejectsAnUnsignedTokenSubstitutedForTheRealOne()
    {
        LogoutTokenFactory factory = this.CreateFactory();
        string token = factory.CreateLogoutToken(SessionId.Create(), "client-1");

        // Strip the signature the way an attacker forging a logout token would.
        string[] segments = token.Split('.');
        string unsigned = $"{segments[0]}.{segments[1]}.";

        TokenValidationResult result = new JsonWebTokenHandler().ValidateTokenAsync(
            unsigned,
            new TokenValidationParameters
            {
                ValidIssuer = ConfiguredIssuer,
                ValidAudience = "client-1",
                IssuerSigningKey = new RsaSecurityKey(this._rsa.ExportParameters(false)),
                ValidateIssuerSigningKey = true,
                ValidateLifetime = false,
            }).GetAwaiter().GetResult();

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void CreateLogoutToken_UsesLogoutTokenTypeHeader()
    {
        LogoutTokenFactory factory = this.CreateFactory();

        string token = factory.CreateLogoutToken(SessionId.Create(), "client-1");

        // 'typ' distinguishes a logout token from an ID token, blocking token substitution.
        new JsonWebTokenHandler().ReadJsonWebToken(token).Typ.ShouldBe("logout+jwt");
    }

    [Fact]
    public void CreateLogoutToken_IncludesRequiredClaims()
    {
        LogoutTokenFactory factory = this.CreateFactory();
        var sessionId = SessionId.Create();

        string token = factory.CreateLogoutToken(sessionId, "client-1");

        JsonWebToken parsed = new JsonWebTokenHandler().ReadJsonWebToken(token);
        parsed.Issuer.ShouldBe(ConfiguredIssuer);
        parsed.Audiences.ShouldContain("client-1");
        parsed.Id.ShouldNotBeNullOrEmpty();
        parsed.GetClaim("sid").Value.ShouldBe(sessionId.Value.ToString());
        parsed.IssuedAt.ShouldBe(this._dateTimeProvider.UtcNow.UtcDateTime);
    }

    [Fact]
    public void CreateLogoutToken_IncludesBackChannelLogoutEvent()
    {
        LogoutTokenFactory factory = this.CreateFactory();

        string token = factory.CreateLogoutToken(SessionId.Create(), "client-1");

        // Read the payload off the wire rather than through a claims abstraction: the
        // specification is specific that 'events' is a JSON object whose single member is the
        // back-channel logout URI mapped to an empty object.
        using JsonDocument payload = ParsePayload(token);
        payload.RootElement.TryGetProperty("events", out JsonElement events).ShouldBeTrue();
        events.ValueKind.ShouldBe(JsonValueKind.Object);
        events.TryGetProperty(BackChannelLogoutEvent, out JsonElement member).ShouldBeTrue();
        member.ValueKind.ShouldBe(JsonValueKind.Object);
    }

    [Fact]
    public void CreateLogoutToken_DoesNotEmitSessionIdAsSubject()
    {
        LogoutTokenFactory factory = this.CreateFactory();
        var sessionId = SessionId.Create();

        string token = factory.CreateLogoutToken(sessionId, "client-1");

        // An earlier revision put the session id in 'sub', making relying parties resolve a
        // principal that does not exist. 'sid' alone satisfies the specification.
        JsonWebToken parsed = new JsonWebTokenHandler().ReadJsonWebToken(token);
        parsed.TryGetClaim("sub", out _).ShouldBeFalse();
    }

    [Fact]
    public void CreateLogoutToken_DoesNotEmitNonce()
    {
        LogoutTokenFactory factory = this.CreateFactory();

        string token = factory.CreateLogoutToken(SessionId.Create(), "client-1");

        // The specification prohibits 'nonce' in a logout token.
        new JsonWebTokenHandler().ReadJsonWebToken(token).TryGetClaim("nonce", out _).ShouldBeFalse();
    }

    [Fact]
    public void CreateLogoutToken_ExpiresShortlyAfterIssuance()
    {
        LogoutTokenFactory factory = this.CreateFactory();

        string token = factory.CreateLogoutToken(SessionId.Create(), "client-1");

        JsonWebToken parsed = new JsonWebTokenHandler().ReadJsonWebToken(token);
        (parsed.ValidTo - parsed.IssuedAt).ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
        parsed.ValidTo.ShouldBeGreaterThan(parsed.IssuedAt);
    }

    [Fact]
    public void CreateLogoutToken_FallsBackToTheRequestIssuerWhenNoneIsConfigured()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("login.example.org");

        LogoutTokenFactory factory = this.CreateFactory(issuer: null, httpContext: httpContext);

        string token = factory.CreateLogoutToken(SessionId.Create(), "client-1");

        new JsonWebTokenHandler().ReadJsonWebToken(token).Issuer.ShouldBe("https://login.example.org");
    }

    [Fact]
    public void CreateLogoutToken_ThrowsWhenNoIssuerCanBeResolved()
    {
        LogoutTokenFactory factory = this.CreateFactory(issuer: null, httpContext: null);

        Should.Throw<InvalidOperationException>(
            () => factory.CreateLogoutToken(SessionId.Create(), "client-1"));
    }

    [Fact]
    public void CreateLogoutToken_ThrowsRatherThanEmitUnsignedTokenWhenNoAsymmetricKeyExists()
    {
        // Fail closed: no usable signing key must not degrade to an unsigned token.
        var options = new OpenIddictServerOptions { Issuer = new Uri(ConfiguredIssuer) };
        options.SigningCredentials.Add(new SigningCredentials(
            new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)),
            SecurityAlgorithms.HmacSha256));

        var factory = new LogoutTokenFactory(
            new StaticOptionsMonitor(options),
            new HttpContextAccessor(),
            this._dateTimeProvider);

        Should.Throw<InvalidOperationException>(
            () => factory.CreateLogoutToken(SessionId.Create(), "client-1"));
    }

    /// <summary>
    /// Base64url-decodes the JWT payload segment so assertions can look at the literal JSON a
    /// relying party receives.
    /// </summary>
    private static JsonDocument ParsePayload(string token)
    {
        string segment = token.Split('.')[1];
        string padded = segment.Replace('-', '+').Replace('_', '/')
            .PadRight(segment.Length + ((4 - (segment.Length % 4)) % 4), '=');
        return JsonDocument.Parse(Convert.FromBase64String(padded));
    }

    private LogoutTokenFactory CreateFactory(
        string? issuer = ConfiguredIssuer,
        HttpContext? httpContext = null)
    {
        var options = new OpenIddictServerOptions
        {
            Issuer = issuer is null ? null : new Uri(issuer),
        };

        options.SigningCredentials.Add(new SigningCredentials(
            new RsaSecurityKey(this._rsa) { KeyId = "test-signing-key" },
            SecurityAlgorithms.RsaSha256));

        return new LogoutTokenFactory(
            new StaticOptionsMonitor(options),
            new HttpContextAccessor { HttpContext = httpContext },
            this._dateTimeProvider);
    }

    private sealed class StaticOptionsMonitor : IOptionsMonitor<OpenIddictServerOptions>
    {
        public StaticOptionsMonitor(OpenIddictServerOptions options) => this.CurrentValue = options;

        public OpenIddictServerOptions CurrentValue { get; }

        public OpenIddictServerOptions Get(string? name) => this.CurrentValue;

        public IDisposable? OnChange(Action<OpenIddictServerOptions, string?> listener) => null;
    }
}
