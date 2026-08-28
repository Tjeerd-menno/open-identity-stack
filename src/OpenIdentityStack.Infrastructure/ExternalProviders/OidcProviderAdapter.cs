using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenIdentityStack.Infrastructure.ExternalProviders;

/// <summary>
/// Adapter for communicating with upstream OIDC providers.
/// </summary>
public sealed class OidcProviderAdapter
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient httpClient;

    public OidcProviderAdapter(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    /// <summary>
    /// Discovers the OpenID Connect configuration for the given authority.
    /// </summary>
    public async Task<OidcDiscoveryDocument?> DiscoverAsync(
        string authority,
        CancellationToken cancellationToken = default)
    {
        string discoveryUrl = $"{authority.TrimEnd('/')}/.well-known/openid-configuration";
        HttpResponseMessage response = await this.httpClient.GetAsync(discoveryUrl, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<OidcDiscoveryDocument>(jsonOptions, cancellationToken);
    }

    /// <summary>
    /// Exchanges an authorization code for tokens.
    /// </summary>
    public async Task<TokenResponse?> ExchangeCodeAsync(
        string tokenEndpoint,
        string code,
        string clientId,
        string? clientSecret,
        string redirectUri,
        string? codeVerifier = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
        };

        if (!string.IsNullOrEmpty(clientSecret))
        {
            parameters["client_secret"] = clientSecret;
        }

        if (!string.IsNullOrEmpty(codeVerifier))
        {
            parameters["code_verifier"] = codeVerifier;
        }

        var content = new FormUrlEncodedContent(parameters);
        HttpResponseMessage response = await this.httpClient.PostAsync(tokenEndpoint, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TokenResponse>(jsonOptions, cancellationToken);
    }

    /// <summary>
    /// Gets user info from the userinfo endpoint.
    /// </summary>
    public async Task<UserInfoResponse?> GetUserInfoAsync(
        string userInfoEndpoint,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, userInfoEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response = await this.httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<UserInfoResponse>(jsonOptions, cancellationToken);
    }
}

/// <summary>
/// OIDC discovery document.
/// </summary>
public sealed record OidcDiscoveryDocument
{
    [JsonPropertyName("issuer")]
    public string Issuer { get; init; } = string.Empty;

    [JsonPropertyName("authorization_endpoint")]
    public string AuthorizationEndpoint { get; init; } = string.Empty;

    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; init; } = string.Empty;

    [JsonPropertyName("userinfo_endpoint")]
    public string? UserInfoEndpoint { get; init; }

    [JsonPropertyName("jwks_uri")]
    public string JwksUri { get; init; } = string.Empty;

    [JsonPropertyName("scopes_supported")]
    public string[] ScopesSupported { get; init; } = [];

    [JsonPropertyName("response_types_supported")]
    public string[] ResponseTypesSupported { get; init; } = [];

    [JsonPropertyName("subject_types_supported")]
    public string[] SubjectTypesSupported { get; init; } = [];

    [JsonPropertyName("id_token_signing_alg_values_supported")]
    public string[] IdTokenSigningAlgValuesSupported { get; init; } = [];
}

/// <summary>
/// Token response from the token endpoint.
/// </summary>
public sealed record TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }
}

/// <summary>
/// User info response from the userinfo endpoint.
/// </summary>
public sealed record UserInfoResponse
{
    [JsonPropertyName("sub")]
    public string Sub { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("email_verified")]
    public bool EmailVerified { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("given_name")]
    public string? GivenName { get; init; }

    [JsonPropertyName("family_name")]
    public string? FamilyName { get; init; }

    [JsonPropertyName("preferred_username")]
    public string? PreferredUsername { get; init; }

    [JsonPropertyName("picture")]
    public string? Picture { get; init; }
}
