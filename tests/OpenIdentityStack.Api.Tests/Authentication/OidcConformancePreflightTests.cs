using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OpenIdentityStack.Api.Tests.Fixtures;

namespace OpenIdentityStack.Api.Tests.Authentication;

[Collection("AppHost")]
public sealed class OidcConformancePreflightTests
{
    private const string PublicIssuer = "https://issuer.example.com/";
    private readonly AppHostFixture fixture;

    public OidcConformancePreflightTests(AppHostFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Discovery_UsesConfiguredPublicIssuerAndEndpointUrls()
    {
        JsonNode metadata = await this.GetDiscoveryMetadataAsync();

        metadata["issuer"]!.GetValue<string>().ShouldBe(PublicIssuer);

        string[] endpointFields =
        [
            "authorization_endpoint",
            "token_endpoint",
            "userinfo_endpoint",
            "jwks_uri"
        ];

        foreach (string field in endpointFields)
        {
            string value = metadata[field]!.GetValue<string>();
            value.ShouldStartWith(PublicIssuer, Case.Sensitive);
            value.ShouldNotContain("localhost");
            value.ShouldNotContain("127.0.0.1");
            value.ShouldNotContain("open-identity-stack");
        }
    }

    [Fact]
    public async Task Discovery_AdvertisesOnlyInitialCertificationResponseType()
    {
        JsonNode metadata = await this.GetDiscoveryMetadataAsync();
        string[] responseTypes = ReadStringArray(metadata, "response_types_supported");

        responseTypes.ShouldContain("code");
        responseTypes.ShouldNotContain("id_token");
        responseTypes.ShouldNotContain("id_token token");
        responseTypes.ShouldNotContain("code id_token");
        responseTypes.ShouldNotContain("code token");
        responseTypes.ShouldNotContain("code id_token token");
    }

    [Fact]
    public async Task Discovery_AdvertisesCoreAuthorizationCodeCapabilities()
    {
        JsonNode metadata = await this.GetDiscoveryMetadataAsync();

        ReadStringArray(metadata, "grant_types_supported").ShouldContain("authorization_code");
        ReadStringArray(metadata, "grant_types_supported").ShouldContain("refresh_token");
        ReadStringArray(metadata, "code_challenge_methods_supported").ShouldContain("S256");
        ReadStringArray(metadata, "subject_types_supported").ShouldContain("public");
        ReadStringArray(metadata, "id_token_signing_alg_values_supported").ShouldContain("RS256");

        string[] scopes = ReadStringArray(metadata, "scopes_supported");
        scopes.ShouldContain("openid");
        scopes.ShouldContain("profile");
        scopes.ShouldContain("email");
        scopes.ShouldContain("offline_access");

        string[] claims = ReadStringArray(metadata, "claims_supported");
        claims.ShouldContain("sub");
        claims.ShouldContain("auth_time");
        claims.ShouldContain("name");
        claims.ShouldContain("given_name");
        claims.ShouldContain("family_name");
        claims.ShouldContain("middle_name");
        claims.ShouldContain("nickname");
        claims.ShouldContain("preferred_username");
        claims.ShouldContain("profile");
        claims.ShouldContain("picture");
        claims.ShouldContain("website");
        claims.ShouldContain("gender");
        claims.ShouldContain("birthdate");
        claims.ShouldContain("zoneinfo");
        claims.ShouldContain("locale");
        claims.ShouldContain("updated_at");
        claims.ShouldContain("email");
        claims.ShouldContain("email_verified");
    }

    [Fact]
    public async Task Discovery_AdvertisesTheAddressAndPhoneScopesAndTheirClaims()
    {
        JsonNode metadata = await this.GetDiscoveryMetadataAsync();

        string[] scopes = ReadStringArray(metadata, "scopes_supported");
        scopes.ShouldContain("address");
        scopes.ShouldContain("phone");

        string[] claims = ReadStringArray(metadata, "claims_supported");
        claims.ShouldContain("address");
        claims.ShouldContain("phone_number");
        claims.ShouldContain("phone_number_verified");
    }

    [Fact]
    public async Task Discovery_AdvertisesS256AsTheOnlyCodeChallengeMethod()
    {
        JsonNode metadata = await this.GetDiscoveryMetadataAsync();

        // "plain" leaves the verifier equal to the challenge, so it protects against
        // nothing. Discovery is what Config OP certifies, so it must advertise S256 only.
        ReadStringArray(metadata, "code_challenge_methods_supported").ShouldBe(["S256"]);
    }

    [Fact]
    public async Task Jwks_ExposesSigningPublicKeysOnly()
    {
        HttpResponseMessage response = await this.fixture.HttpClient!.GetAsync("/.well-known/jwks");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonNode? jwks = await response.Content.ReadFromJsonAsync<JsonNode>();
        jwks.ShouldNotBeNull();

        JsonArray keys = jwks!["keys"]!.AsArray();
        keys.Count.ShouldBeGreaterThan(0);

        foreach (JsonNode? key in keys)
        {
            key.ShouldNotBeNull();
            key!["kid"].ShouldNotBeNull();
            key["kty"].ShouldNotBeNull();
            key["d"].ShouldBeNull();
            key["p"].ShouldBeNull();
            key["q"].ShouldBeNull();
        }
    }

    private async Task<JsonNode> GetDiscoveryMetadataAsync()
    {
        HttpResponseMessage response = await this.fixture.HttpClient!.GetAsync("/.well-known/openid-configuration");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonNode? metadata = await response.Content.ReadFromJsonAsync<JsonNode>();
        metadata.ShouldNotBeNull();
        return metadata!;
    }

    private static string[] ReadStringArray(JsonNode metadata, string field)
    {
        JsonArray? values = metadata[field]?.AsArray();
        values.ShouldNotBeNull();
        return values!.Select(value => value!.GetValue<string>()).ToArray();
    }
}
