
using System.Net;
using System.Text.Json;
using OpenIdentityStack.Infrastructure.ExternalProviders;

namespace OpenIdentityStack.Infrastructure.Tests.ExternalProviders;
/// <summary>
/// Unit tests for the OidcProviderAdapter.
/// </summary>
public sealed class OidcProviderAdapterTests : IDisposable
{
    private readonly OidcProviderAdapter _adapter;
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly HttpClient _httpClient;

    public OidcProviderAdapterTests()
    {
        this._mockHandler = new MockHttpMessageHandler();
        this._httpClient = new HttpClient(this._mockHandler)
        {
            BaseAddress = new Uri("https://test.example.com")
        };
        this._adapter = new OidcProviderAdapter(this._httpClient);
    }

    public void Dispose()
    {
        this._httpClient.Dispose();
        this._mockHandler.Dispose();
    }

    [Fact]
    public async Task DiscoverAsync_WithValidAuthority_ReturnsDiscoveryDocument()
    {
        // Arrange
        var discoveryDoc = new OidcDiscoveryDocument
        {
            Issuer = "https://issuer.example.com",
            AuthorizationEndpoint = "https://issuer.example.com/authorize",
            TokenEndpoint = "https://issuer.example.com/token",
            JwksUri = "https://issuer.example.com/.well-known/jwks.json",
            ScopesSupported = ["openid", "profile", "email"],
            ResponseTypesSupported = ["code"],
            SubjectTypesSupported = ["public"],
            IdTokenSigningAlgValuesSupported = ["RS256"]
        };

        this._mockHandler.SetResponse(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(discoveryDoc, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));

        // Act
        OidcDiscoveryDocument? result = await this._adapter.DiscoverAsync("https://issuer.example.com");

        // Assert
        result.ShouldNotBeNull();
        result.Issuer.ShouldBe("https://issuer.example.com");
        result.TokenEndpoint.ShouldBe("https://issuer.example.com/token");
    }

    [Fact]
    public async Task DiscoverAsync_WithInvalidAuthority_ReturnsNull()
    {
        // Arrange
        this._mockHandler.SetResponse(HttpStatusCode.NotFound, "Not Found");

        // Act
        OidcDiscoveryDocument? result = await this._adapter.DiscoverAsync("https://invalid.example.com");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DiscoverAsync_WithTrailingSlash_ConstructsCorrectUrl()
    {
        // Arrange
        this._mockHandler.SetResponse(HttpStatusCode.OK, "{}");

        // Act
        await this._adapter.DiscoverAsync("https://issuer.example.com/");

        // Assert
        this._mockHandler.LastRequestUri.ShouldBe("https://issuer.example.com/.well-known/openid-configuration");
    }

    [Fact]
    public async Task ExchangeCodeAsync_WithValidCode_ReturnsTokenResponse()
    {
        // Arrange
        var tokenResponse = new
        {
            access_token = "access-token-123",
            token_type = "Bearer",
            expires_in = 3600,
            refresh_token = "refresh-token-456",
            id_token = "id-token-789"
        };

        this._mockHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(tokenResponse));

        // Act
        TokenResponse? result = await this._adapter.ExchangeCodeAsync(
            "https://issuer.example.com/token",
            "auth-code",
            "client-id",
            "client-secret",
            "https://app.example.com/callback");

        // Assert
        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe("access-token-123");
        result.TokenType.ShouldBe("Bearer");
        result.ExpiresIn.ShouldBe(3600);
        result.RefreshToken.ShouldBe("refresh-token-456");
        result.IdToken.ShouldBe("id-token-789");
    }

    [Fact]
    public async Task ExchangeCodeAsync_WithInvalidCode_ReturnsNull()
    {
        // Arrange
        this._mockHandler.SetResponse(HttpStatusCode.BadRequest, "{\"error\": \"invalid_grant\"}");

        // Act
        TokenResponse? result = await this._adapter.ExchangeCodeAsync(
            "https://issuer.example.com/token",
            "invalid-code",
            "client-id",
            "client-secret",
            "https://app.example.com/callback");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExchangeCodeAsync_WithCodeVerifier_IncludesPkceParameter()
    {
        // Arrange
        this._mockHandler.SetResponse(HttpStatusCode.OK, "{\"access_token\": \"token\", \"token_type\": \"Bearer\", \"expires_in\": 3600}");

        // Act
        await this._adapter.ExchangeCodeAsync(
            "https://issuer.example.com/token",
            "auth-code",
            "client-id",
            null,
            "https://app.example.com/callback",
            "code-verifier-123");

        // Assert
        this._mockHandler.LastRequestContent!.ShouldContain("code_verifier=code-verifier-123");
    }

    [Fact]
    public async Task GetUserInfoAsync_WithValidAccessToken_ReturnsUserInfo()
    {
        // Arrange
        var userInfoResponse = new
        {
            sub = "user-subject-123",
            email = "user@example.com",
            email_verified = true,
            name = "Test User",
            given_name = "Test",
            family_name = "User"
        };

        this._mockHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(userInfoResponse));

        // Act
        UserInfoResponse? result = await this._adapter.GetUserInfoAsync(
            "https://issuer.example.com/userinfo",
            "access-token");

        // Assert
        result.ShouldNotBeNull();
        result.Sub.ShouldBe("user-subject-123");
        result.Email.ShouldBe("user@example.com");
        result.EmailVerified.ShouldBeTrue();
        result.Name.ShouldBe("Test User");
    }

    [Fact]
    public async Task GetUserInfoAsync_WithInvalidToken_ReturnsNull()
    {
        // Arrange
        this._mockHandler.SetResponse(HttpStatusCode.Unauthorized, "Unauthorized");

        // Act
        UserInfoResponse? result = await this._adapter.GetUserInfoAsync(
            "https://issuer.example.com/userinfo",
            "invalid-token");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetUserInfoAsync_IncludesBearerToken()
    {
        // Arrange
        this._mockHandler.SetResponse(HttpStatusCode.OK, "{\"sub\": \"user-123\"}");

        // Act
        await this._adapter.GetUserInfoAsync(
            "https://issuer.example.com/userinfo",
            "my-access-token");

        // Assert
        this._mockHandler.LastRequestAuthorizationHeader.ShouldBe("Bearer my-access-token");
    }

    [Fact]
    public async Task ExchangeCodeAsync_WithoutClientSecret_DoesNotIncludeSecret()
    {
        // Arrange
        this._mockHandler.SetResponse(HttpStatusCode.OK, "{\"access_token\": \"token\", \"token_type\": \"Bearer\", \"expires_in\": 3600}");

        // Act
        await this._adapter.ExchangeCodeAsync(
            "https://issuer.example.com/token",
            "auth-code",
            "client-id",
            null, // No secret
            "https://app.example.com/callback");

        // Assert
        this._mockHandler.LastRequestContent!.ShouldNotContain("client_secret");
    }

    /// <summary>
    /// Mock HTTP message handler for testing.
    /// </summary>
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private HttpStatusCode _statusCode = HttpStatusCode.OK;
        private string _responseContent = "{}";

        public string? LastRequestUri { get; private set; }
        public string? LastRequestContent { get; private set; }
        public string? LastRequestAuthorizationHeader { get; private set; }

        public void SetResponse(HttpStatusCode statusCode, string content)
        {
            this._statusCode = statusCode;
            this._responseContent = content;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            this.LastRequestUri = request.RequestUri?.ToString();
            this.LastRequestAuthorizationHeader = request.Headers.Authorization?.ToString();

            if (request.Content is not null)
            {
                this.LastRequestContent = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(this._statusCode)
            {
                Content = new StringContent(this._responseContent, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
