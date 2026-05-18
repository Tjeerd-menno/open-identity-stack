using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using OpenIdentityStack.Api.Tests.Fixtures;

namespace OpenIdentityStack.Api.Tests.Authentication;

/// <summary>
/// Integration tests for the authorization code flow with PKCE.
/// Uses WebApplicationFactory test infrastructure.
/// 
/// NOTE: Some tests are skipped because the authorization endpoint needs proper
/// test client registration for full OIDC flow testing.
/// </summary>
[Collection("AppHost")]
public sealed class AuthorizationCodeFlowTests
{
    private readonly AppHostFixture _fixture;

    public AuthorizationCodeFlowTests(AppHostFixture fixture)
    {
        this._fixture = fixture;
    }

    #region Discovery Endpoint

    [Fact]
    public async Task Discovery_ReturnsOpenIdConfiguration()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/.well-known/openid-configuration");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("issuer");
        content.ShouldContain("authorization_endpoint");
        content.ShouldContain("token_endpoint");
        content.ShouldContain("userinfo_endpoint");
    }

    [Fact]
    public async Task Discovery_ContainsRequiredEndpoints()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/.well-known/openid-configuration");
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();

        // Assert
        json!["issuer"].ShouldNotBeNull();
        json["authorization_endpoint"].ShouldNotBeNull();
        json["token_endpoint"].ShouldNotBeNull();
        json["jwks_uri"].ShouldNotBeNull();
    }

    [Fact]
    public async Task Discovery_ListsSupportedGrantTypes()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/.well-known/openid-configuration");
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();

        // Assert
        JsonArray? grantTypes = json!["grant_types_supported"]?.AsArray();
        grantTypes.ShouldNotBeNull();
        grantTypes.Select(g => g!.GetValue<string>()).ShouldContain("authorization_code");
        grantTypes.Select(g => g!.GetValue<string>()).ShouldContain("client_credentials");
    }

    #endregion

    #region Authorization Endpoint

    [Fact]
    public async Task Authorize_WithoutAuthentication_RedirectsToLogin()
    {
        // Arrange - Register an authorization code client first
        string testClientId = $"auth-test-{Guid.NewGuid():N}";
        await this._fixture.CreateServiceAccountAsync(
            testClientId,
            "test-secret",
            allowedScopes: ["openid"],
            allowedGrantTypes: ["authorization_code"],
            redirectUris: ["https://localhost/callback"]);

        string codeVerifier = GenerateCodeVerifier();
        string codeChallenge = GenerateCodeChallenge(codeVerifier);
        
        using HttpClient client = this._fixture.CreateClient(allowAutoRedirect: false);

        // Act
        HttpResponseMessage response = await client.GetAsync($"/connect/authorize?response_type=code&client_id={testClientId}&redirect_uri=https://localhost/callback&scope=openid&code_challenge={codeChallenge}&code_challenge_method=S256");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().ShouldContain("/Account/Login");
    }

    [Fact]
    public async Task Authorize_RedirectPreservesReturnUrl()
    {
        // Arrange - Register an authorization code client first
        string testClientId = $"auth-test-{Guid.NewGuid():N}";
        await this._fixture.CreateServiceAccountAsync(
            testClientId,
            "test-secret",
            allowedScopes: ["openid"],
            allowedGrantTypes: ["authorization_code"],
            redirectUris: ["https://localhost/callback"]);

        string codeVerifier = GenerateCodeVerifier();
        string codeChallenge = GenerateCodeChallenge(codeVerifier);
        
        using HttpClient client = this._fixture.CreateClient(allowAutoRedirect: false);

        // Act
        HttpResponseMessage response = await client.GetAsync($"/connect/authorize?response_type=code&client_id={testClientId}&redirect_uri=https://localhost/callback&scope=openid&code_challenge={codeChallenge}&code_challenge_method=S256");

        // Assert
        response.Headers.Location.ShouldNotBeNull();
        string location = response.Headers.Location.ToString();
        location.ShouldContain("returnUrl");;
    }

    [Fact]
    public async Task Authorize_WithUnsupportedRequestObject_RedirectsBackWithRequestNotSupportedError()
    {
        // Arrange
        string clientId = $"request-object-{Guid.NewGuid():N}";
        const string redirectUri = "https://localhost/callback";
        const string state = "request-object-state";

        await this._fixture.CreateServiceAccountAsync(
            clientId,
            "test-secret",
            allowedScopes: ["openid"],
            allowedGrantTypes: ["authorization_code"],
            redirectUris: [redirectUri]);

        using HttpClient client = this._fixture.CreateClient(allowAutoRedirect: false);

        string requestObject = CreateUnsignedRequestObject(clientId, redirectUri, state);
        string authorizeUrl =
            $"/connect/authorize?client_id={Uri.EscapeDataString(clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&request={Uri.EscapeDataString(requestObject)}" +
            "&response_type=code&scope=openid";

        // Act
        HttpResponseMessage response = await client.GetAsync(authorizeUrl);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.GetLeftPart(UriPartial.Path).ShouldBe(redirectUri);

        Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query =
            QueryHelpers.ParseQuery(response.Headers.Location.Query);

        query["error"].Single().ShouldBe("request_not_supported");
        query["state"].Single().ShouldBe(state);
    }

    #endregion

    #region Login Endpoint

    [Fact]
    public async Task Login_Get_ReturnsLoginPage()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/Login");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string content = await response.Content.ReadAsStringAsync();
        // The response should contain a form for login
        content.ShouldContain("form");
    }

    #endregion

    #region Token Endpoint

    [Fact]
    public async Task AuthorizationCodeFlow_WithMaxAge_IncludesNumericAuthTimeInIdToken()
    {
        // Arrange
        string clientId = $"max-age-client-{Guid.NewGuid():N}";
        string clientSecret = "test-secret-123!";
        string redirectUri = "https://localhost/callback";
        string email = $"max-age-{Guid.NewGuid():N}@example.test";
        string password = "Password123!@#";

        await this._fixture.CreateTestUserAsync(email, "Max Age User", password);
        await this._fixture.CreateServiceAccountAsync(
            clientId,
            clientSecret,
            allowedScopes: ["openid"],
            allowedGrantTypes: ["authorization_code"],
            redirectUris: [redirectUri]);

        string codeVerifier = GenerateCodeVerifier();
        string codeChallenge = GenerateCodeChallenge(codeVerifier);

        using HttpClient client = this._fixture.CreateClient(allowAutoRedirect: false);

        string authorizeQuery = await new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = "openid",
            ["state"] = "state-123",
            ["nonce"] = "nonce-123",
            ["max_age"] = "300",
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        }).ReadAsStringAsync();
        string authorizeUrl = "/connect/authorize?" + authorizeQuery;

        // Act
        HttpResponseMessage authorizeResponse = await client.GetAsync(authorizeUrl);
        authorizeResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        authorizeResponse.Headers.Location.ShouldNotBeNull();
        Uri loginUri = authorizeResponse.Headers.Location;

        HttpResponseMessage loginPageResponse = await client.GetAsync(loginUri);
        loginPageResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        string loginPage = await loginPageResponse.Content.ReadAsStringAsync();
        string antiForgeryToken = ExtractAntiForgeryToken(loginPage);

        HttpResponseMessage loginResponse = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["RememberMe"] = "false",
            ["returnUrl"] = QueryHelpers.ParseQuery(GetQuery(loginUri))["returnUrl"].Single()!,
            ["__RequestVerificationToken"] = antiForgeryToken
        }));
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        HttpResponseMessage callbackResponse = await client.GetAsync(loginResponse.Headers.Location);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callbackResponse.Headers.Location.ShouldNotBeNull();
        Uri callbackUri = callbackResponse.Headers.Location;
        string code = QueryHelpers.ParseQuery(callbackUri.Query)["code"].Single()!;

        HttpResponseMessage tokenResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        }));

        // Assert
        tokenResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonNode>();
        string idToken = tokenJson?["id_token"]?.GetValue<string>() ?? throw new InvalidOperationException("ID token not returned.");
        JsonNode payload = ReadJwtPayload(idToken);

        payload["auth_time"].ShouldNotBeNull();
        payload["auth_time"]!.GetValue<JsonElement>().ValueKind.ShouldBe(JsonValueKind.Number);
        payload["auth_time"]!.GetValue<long>().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Token_WithInvalidGrant_ReturnsError()
    {
        // Arrange - Register an authorization code client first
        string testClientId = $"token-test-{Guid.NewGuid():N}";
        await this._fixture.CreateServiceAccountAsync(
            testClientId,
            "test-secret",
            allowedScopes: ["openid"],
            allowedGrantTypes: ["authorization_code"],
            redirectUris: ["https://localhost/callback"]);

        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = "invalid_code",
            ["redirect_uri"] = "https://localhost/callback",
            ["code_verifier"] = GenerateCodeVerifier(),
            ["client_id"] = testClientId
        }));

        // Assert - OpenIddict returns 401 for invalid authorization codes
        // (400 for missing parameters, 401 for invalid credentials/codes)
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
        string content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("error");
    }

    [Fact]
    public async Task Token_WithMissingCode_ReturnsError()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = "https://localhost/callback",
            ["code_verifier"] = GenerateCodeVerifier(),
            ["client_id"] = "test-client"
        }));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region PKCE Contract Tests

    [Fact]
    public void CodeVerifier_MeetsRFC7636Requirements()
    {
        // RFC 7636 Section 4.1: code_verifier is 43-128 characters
        string codeVerifier = GenerateCodeVerifier();
        
        codeVerifier.Length.ShouldBeGreaterThanOrEqualTo(43);
        codeVerifier.Length.ShouldBeLessThanOrEqualTo(128);
    }

    [Fact]
    public void CodeChallenge_IsDerivedFromVerifier()
    {
        string codeVerifier = GenerateCodeVerifier();
        string codeChallenge = GenerateCodeChallenge(codeVerifier);

        // Challenge should be different from verifier
        codeChallenge.ShouldNotBe(codeVerifier);

        // Challenge should be consistent for same verifier
        string codeChallenge2 = GenerateCodeChallenge(codeVerifier);
        codeChallenge2.ShouldBe(codeChallenge);
    }

    [Fact]
    public void CodeChallenge_IsBase64UrlEncoded()
    {
        string codeVerifier = GenerateCodeVerifier();
        string codeChallenge = GenerateCodeChallenge(codeVerifier);

        // Should not contain + or / (base64 chars replaced in URL encoding)
        codeChallenge.ShouldNotContain("+");
        codeChallenge.ShouldNotContain("/");
        
        // Should not end with = (padding removed)
        codeChallenge.ShouldNotEndWith("=");
    }

    #endregion

    #region Logout Endpoint

    [Fact]
    public async Task Logout_ReturnsRedirect()
    {
        // Arrange
        using HttpClient client = this._fixture.CreateClient(allowAutoRedirect: false);

        // Act
        HttpResponseMessage response = await client.GetAsync("/connect/logout");

        // Assert
        // Logout should redirect to home or specified URI
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Redirect, HttpStatusCode.OK);
    }

    #endregion

    #region UserInfo Endpoint

    [Fact]
    public async Task UserInfo_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/connect/userinfo");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UserInfo_WithValidServiceAccountToken_ReturnsUserInfo()
    {
        // Arrange
        string clientId = $"userinfo-client-{Guid.NewGuid():N}";
        string clientSecret = "test-secret-123!";
        HttpClient client = await this._fixture.CreateAuthenticatedClientAsync(clientId, clientSecret);

        // Act
        HttpResponseMessage response = await client.GetAsync("/connect/userinfo");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("sub");
    }

    #endregion

    #region PKCE Helpers

    private static string GenerateCodeVerifier()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        byte[] bytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        Match match = Regex.Match(
            html,
            "<input[^>]+name=\"__RequestVerificationToken\"[^>]+value=\"(?<value>[^\"]+)\"",
            RegexOptions.IgnoreCase);

        match.Success.ShouldBeTrue("The login form should render an anti-forgery token.");
        return WebUtility.HtmlDecode(match.Groups["value"].Value);
    }

    private static string GetQuery(Uri uri)
    {
        if (uri.IsAbsoluteUri)
        {
            return uri.Query;
        }

        int queryStart = uri.OriginalString.IndexOf('?', StringComparison.Ordinal);
        return queryStart < 0 ? string.Empty : uri.OriginalString[queryStart..];
    }

    private static JsonNode ReadJwtPayload(string token)
    {
        string[] parts = token.Split('.');
        parts.Length.ShouldBeGreaterThanOrEqualTo(2);

        byte[] payloadBytes = WebEncoders.Base64UrlDecode(parts[1]);
        return JsonNode.Parse(Encoding.UTF8.GetString(payloadBytes)) ?? throw new InvalidOperationException("The JWT payload could not be parsed.");
    }

    private static string CreateUnsignedRequestObject(string clientId, string redirectUri, string state)
    {
        static string Encode(object value)
        {
            byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(value);
            return WebEncoders.Base64UrlEncode(jsonBytes);
        }

        string header = Encode(new { alg = "none" });
        string payload = Encode(new
        {
            scope = "openid",
            response_type = "code",
            redirect_uri = redirectUri,
            state,
            nonce = "nonce-request-object",
            client_id = clientId
        });

        return $"{header}.{payload}.";
    }

    #endregion
}
