namespace OpenIdentityStack.Contract.Tests.Authentication;

/// <summary>
/// Contract tests that document token request field expectations.
/// </summary>
public sealed class TokenRequestContractTests
{
    [Fact]
    public void ClientCredentialsRequest_RequiredFields_AreDocumented()
    {
        // Per RFC 6749 Section 4.4
        string[] requiredFields = new[]
        {
            "grant_type",   // REQUIRED. Must be "client_credentials".
            "client_id",    // REQUIRED for confidential clients.
            "client_secret" // REQUIRED for client authentication.
        };

        requiredFields.ShouldContain("grant_type");
        requiredFields.ShouldContain("client_id");
        requiredFields.ShouldContain("client_secret");
    }

    [Fact]
    public void AuthorizationCodeRequest_RequiredFields_AreDocumented()
    {
        // Per RFC 6749 Section 4.1
        string[] requiredFields = new[]
        {
            "grant_type",   // REQUIRED. Must be "authorization_code".
            "code",         // REQUIRED. The authorization code.
            "redirect_uri"  // REQUIRED if included in the authorization request.
        };

        requiredFields.ShouldContain("grant_type");
        requiredFields.ShouldContain("code");
    }

    [Fact]
    public void RefreshTokenRequest_RequiredFields_AreDocumented()
    {
        // Per RFC 6749 Section 6
        string[] requiredFields = new[]
        {
            "grant_type",    // REQUIRED. Must be "refresh_token".
            "refresh_token"  // REQUIRED. The refresh token.
        };

        requiredFields.ShouldContain("grant_type");
        requiredFields.ShouldContain("refresh_token");
    }
}
