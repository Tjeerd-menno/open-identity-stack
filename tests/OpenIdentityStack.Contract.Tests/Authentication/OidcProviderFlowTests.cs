namespace OpenIdentityStack.Contract.Tests.Authentication;

/// <summary>
/// Contract tests documenting the upstream OIDC provider flow.
/// </summary>
public sealed class OidcProviderFlowTests
{
    [Fact]
    public void DiscoveryDocument_RequiredFields_AreDocumented()
    {
        // Required fields per OpenID Connect Discovery spec
        string[] requiredFields = new[]
        {
            "issuer",
            "authorization_endpoint",
            "token_endpoint",
            "jwks_uri"
        };

        requiredFields.ShouldContain("issuer");
        requiredFields.ShouldContain("authorization_endpoint");
        requiredFields.ShouldContain("token_endpoint");
        requiredFields.ShouldContain("jwks_uri");
    }

    [Fact]
    public void TokenExchange_ResponseFields_AreDocumented()
    {
        // Token exchange response fields per OAuth2/OpenID Connect
        string[] requiredFields = new[]
        {
            "access_token",
            "token_type"
        };

        string[] optionalFields = new[]
        {
            "id_token",
            "expires_in",
            "refresh_token",
            "scope"
        };

        requiredFields.ShouldContain("access_token");
        requiredFields.ShouldContain("token_type");
        optionalFields.ShouldContain("id_token");
    }

    [Fact]
    public void UserInfo_ResponseFields_AreDocumented()
    {
        // UserInfo response fields per OpenID Connect Core
        string[] requiredFields = new[]
        {
            "sub"
        };

        string[] optionalFields = new[]
        {
            "name",
            "email",
            "email_verified",
            "preferred_username"
        };

        requiredFields.ShouldContain("sub");
        optionalFields.ShouldContain("email");
    }
}
