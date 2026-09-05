using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
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

    [Theory]
    [InlineData(false, "access_denied")]
    [InlineData(true, "invalid_target")]
    public async Task Authorize_ResourceDenied_RedirectsWithProtocolErrorAndOriginalState(bool unknownResource, string expectedError)
    {
        string clientId = $"resource-denied-{Guid.NewGuid():N}";
        string email = $"resource-denied-{Guid.NewGuid():N}@example.test";
        const string password = "Password123!@#";
        const string redirectUri = "https://localhost/callback";
        const string state = "resource-denial-state";
        await this._fixture.CreateTestUserAsync(email, "Resource user", password);
        await this._fixture.CreateServiceAccountAsync(clientId, "test-secret", ["openid", "ois.admin"],
            allowedGrantTypes: ["authorization_code"], redirectUris: [redirectUri]);
        await this._fixture.ExecuteDbContextAsync(async db =>
        {
            if (!await db.ProtectedResources.AnyAsync(value => value.Id == Domain.Resources.ProtectedResource.AdministrativeResourceId))
            {
                db.ProtectedResources.Add(Domain.Resources.ProtectedResource.CreateAdministrative());
            }
            Domain.Applications.Application application = await db.Applications.SingleAsync(value => value.ClientId == clientId);
            db.ClientResourceGrants.RemoveRange(await db.ClientResourceGrants.Where(value => value.ClientApplicationId == application.Id).ToListAsync());
            await db.SaveChangesAsync();
        });
        using HttpClient client = this._fixture.CreateClient(allowAutoRedirect: false);
        var parameters = new Dictionary<string, string>
        {
            ["response_type"] = "code", ["client_id"] = clientId, ["redirect_uri"] = redirectUri,
            ["scope"] = "openid ois.admin", ["state"] = state,
            ["code_challenge"] = GenerateCodeChallenge(GenerateCodeVerifier()), ["code_challenge_method"] = "S256"
        };
        if (unknownResource) { parameters["resource"] = "https://unknown.example.com/api"; }
        string query = await new FormUrlEncodedContent(parameters).ReadAsStringAsync();
        using HttpResponseMessage loginPage = await client.GetAsync("/Account/Login");
        using HttpResponseMessage login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = password, ["RememberMe"] = "false",
            ["returnUrl"] = "/connect/authorize?" + query,
            ["__RequestVerificationToken"] = ExtractAntiForgeryToken(await loginPage.Content.ReadAsStringAsync())
        }));
        using HttpResponseMessage callback = await client.GetAsync(login.Headers.Location);

        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callback.Headers.Location!.GetLeftPart(UriPartial.Path).ShouldBe(redirectUri);
        Dictionary<string, Microsoft.Extensions.Primitives.StringValues> response = QueryHelpers.ParseQuery(callback.Headers.Location.Query);
        response["error"].Single().ShouldBe(expectedError);
        response["state"].Single().ShouldBe(state);
        response.ContainsKey("code").ShouldBeFalse();
    }

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdministrativeDisablement_BlocksExistingCookieAndOutstandingGrant(bool refresh)
    {
        string clientId = $"disablement-{Guid.NewGuid():N}";
        const string clientSecret = "test-secret-123!";
        const string redirectUri = "https://localhost/callback";
        string email = $"disablement-{Guid.NewGuid():N}@example.test";
        const string password = "Password123!@#";
        Guid userId = await this._fixture.CreateTestUserAsync(email, "Disablement User", password);
        await this._fixture.CreateServiceAccountAsync(clientId, clientSecret,
            allowedScopes: ["openid", "offline_access"],
            allowedGrantTypes: ["authorization_code", "refresh_token"], redirectUris: [redirectUri]);
        string verifier = GenerateCodeVerifier();
        string query = await new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["response_type"] = "code", ["client_id"] = clientId, ["redirect_uri"] = redirectUri,
            ["scope"] = "openid offline_access", ["state"] = "disablement-state",
            ["code_challenge"] = GenerateCodeChallenge(verifier), ["code_challenge_method"] = "S256"
        }).ReadAsStringAsync();
        string authorizeUrl = "/connect/authorize?" + query;
        using HttpClient browser = this._fixture.CreateClient(allowAutoRedirect: false);
        HttpResponseMessage start = await browser.GetAsync(authorizeUrl);
        Uri loginUri = start.Headers.Location!;
        string loginPage = await browser.GetStringAsync(loginUri);
        HttpResponseMessage login = await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = password,
            ["returnUrl"] = QueryHelpers.ParseQuery(GetQuery(loginUri))["returnUrl"].Single()!,
            ["__RequestVerificationToken"] = ExtractAntiForgeryToken(loginPage)
        }));
        login.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        HttpResponseMessage authorization = await browser.GetAsync(login.Headers.Location);
        authorization.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        string code = QueryHelpers.ParseQuery(authorization.Headers.Location!.Query)["code"].Single()!;
        var grant = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["client_id"] = clientId,
            ["client_secret"] = clientSecret, ["code"] = code, ["code_verifier"] = verifier, ["redirect_uri"] = redirectUri
        };
        if (refresh)
        {
            HttpResponseMessage tokenResponse = await browser.PostAsync("/connect/token", new FormUrlEncodedContent(grant));
            tokenResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            JsonNode? tokens = await tokenResponse.Content.ReadFromJsonAsync<JsonNode>();
            grant = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token", ["client_id"] = clientId, ["client_secret"] = clientSecret,
                ["refresh_token"] = tokens!["refresh_token"]!.GetValue<string>()
            };
        }

        using HttpClient administrator = await this._fixture.CreateAuthenticatedClientAsync($"disable-admin-{Guid.NewGuid():N}", "test-secret");
        HttpResponseMessage disable = await administrator.PostAsJsonAsync($"/api/admin/users/{userId}/disable", new { Reason = "Administrative decision" });
        disable.IsSuccessStatusCode.ShouldBeTrue();

        HttpResponseMessage exchange = await browser.PostAsync("/connect/token", new FormUrlEncodedContent(grant));
        exchange.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonNode? error = await exchange.Content.ReadFromJsonAsync<JsonNode>();
        error!["error"]!.GetValue<string>().ShouldBe("invalid_grant");
        error["error_description"]!.GetValue<string>().ShouldBe("The credentials are no longer valid.");
        HttpResponseMessage cookieReuse = await browser.GetAsync(authorizeUrl);
        cookieReuse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        cookieReuse.Headers.Location!.ToString().ShouldContain("/Account/Login");

        HttpResponseMessage enable = await administrator.PostAsync($"/api/admin/users/{userId}/enable", null);
        enable.IsSuccessStatusCode.ShouldBeTrue();
        HttpResponseMessage authorizedAgain = await browser.GetAsync(authorizeUrl);
        authorizedAgain.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        authorizedAgain.Headers.Location!.ToString().ShouldContain("/Account/Login");
        // Re-enablement does not resurrect a rejected cookie; prove local control again.
        string freshLoginPage = await browser.GetStringAsync(authorizedAgain.Headers.Location);
        HttpResponseMessage freshLogin = await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = password, ["returnUrl"] = authorizeUrl,
            ["__RequestVerificationToken"] = ExtractAntiForgeryToken(freshLoginPage)
        }));
        HttpResponseMessage freshAuthorization = await browser.GetAsync(freshLogin.Headers.Location);
        QueryHelpers.ParseQuery(freshAuthorization.Headers.Location!.Query)["code"].Single().ShouldNotBeNullOrWhiteSpace();
    }

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
    public async Task Authorize_WithPlainCodeChallengeMethod_IsRejected()
    {
        // Arrange - advertising S256 only would be worse than the status quo if
        // "plain" were still silently honoured, so the reject path is the real guard.
        string testClientId = $"plain-pkce-{Guid.NewGuid():N}";
        await this._fixture.CreateServiceAccountAsync(
            testClientId,
            "test-secret",
            allowedScopes: ["openid"],
            allowedGrantTypes: ["authorization_code"],
            redirectUris: ["https://localhost/callback"]);

        string codeVerifier = GenerateCodeVerifier();

        using HttpClient client = this._fixture.CreateClient(allowAutoRedirect: false);

        // Act - with "plain" the challenge is the verifier itself
        HttpResponseMessage response = await client.GetAsync(
            $"/connect/authorize?response_type=code&client_id={testClientId}&redirect_uri=https://localhost/callback&scope=openid&code_challenge={codeVerifier}&code_challenge_method=plain");

        // Assert - rejected without ever reaching the login page. The client_id and
        // redirect_uri are both valid, so per OIDC Core §3.1.2.6 the rejection is handed
        // back to the RP as an error redirect rather than a 400 page (see #330).
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.GetLeftPart(UriPartial.Path).ShouldBe("https://localhost/callback");

        Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query =
            QueryHelpers.ParseQuery(response.Headers.Location.Query);

        query["error"].Single().ShouldBe("invalid_request");
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

    [Fact]
    public async Task Authorize_WithAuthenticatedSession_IncludesSessionStateInCallbackRedirect()
    {
        // Arrange
        string clientId = $"session-state-client-{Guid.NewGuid():N}";
        string clientSecret = "test-secret-123!";
        string redirectUri = "https://localhost/callback";
        string email = $"session-state-{Guid.NewGuid():N}@example.test";
        string password = "Password123!@#";

        await this._fixture.CreateTestUserAsync(email, "Session State User", password);
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
            ["state"] = "session-state-123",
            ["nonce"] = "session-state-nonce",
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        }).ReadAsStringAsync();

        // Act
        HttpResponseMessage authorizeResponse = await client.GetAsync("/connect/authorize?" + authorizeQuery);
        authorizeResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        authorizeResponse.Headers.Location.ShouldNotBeNull();

        HttpResponseMessage loginPageResponse = await client.GetAsync(authorizeResponse.Headers.Location);
        loginPageResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        string loginPage = await loginPageResponse.Content.ReadAsStringAsync();
        string antiForgeryToken = ExtractAntiForgeryToken(loginPage);

        HttpResponseMessage loginResponse = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["RememberMe"] = "false",
            ["returnUrl"] = QueryHelpers.ParseQuery(GetQuery(authorizeResponse.Headers.Location))["returnUrl"].Single()!,
            ["__RequestVerificationToken"] = antiForgeryToken
        }));
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        HttpResponseMessage callbackResponse = await client.GetAsync(loginResponse.Headers.Location);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callbackResponse.Headers.Location.ShouldNotBeNull();

        // Assert
        callbackResponse.Headers.Location.GetLeftPart(UriPartial.Path).ShouldBe(redirectUri);

        Dictionary<string, Microsoft.Extensions.Primitives.StringValues> callbackQuery =
            QueryHelpers.ParseQuery(callbackResponse.Headers.Location.Query);

        callbackQuery.ShouldContainKey("session_state");
        callbackQuery["session_state"].Single().ShouldNotBeNullOrWhiteSpace();
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
