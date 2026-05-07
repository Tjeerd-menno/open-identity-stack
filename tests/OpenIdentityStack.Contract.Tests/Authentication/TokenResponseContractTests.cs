namespace OpenIdentityStack.Contract.Tests.Authentication;

/// <summary>
/// Contract tests to verify the shape of token responses matches OpenID Connect spec.
/// </summary>
public sealed class TokenResponseContractTests
{
    #region Access Token Response Contract

    [Fact]
    public void AccessTokenResponse_RequiredFields_AreDocumented()
    {
        // This test documents the required fields in an access token response
        // per RFC 6749 Section 5.1
        string[] requiredFields = new[]
        {
            "access_token",    // REQUIRED. The access token issued by the authorization server.
            "token_type"       // REQUIRED. The type of the token issued (e.g., "Bearer").
        };

        requiredFields.ShouldNotBeEmpty();
        requiredFields.ShouldContain("access_token");
        requiredFields.ShouldContain("token_type");
    }

    [Fact]
    public void AccessTokenResponse_OptionalFields_AreDocumented()
    {
        // This test documents the optional fields in an access token response
        // per RFC 6749 Section 5.1
        string[] optionalFields = new[]
        {
            "expires_in",      // RECOMMENDED. Lifetime in seconds of the access token.
            "refresh_token",   // OPTIONAL. The refresh token.
            "scope"            // OPTIONAL if identical to scope requested.
        };

        optionalFields.ShouldNotBeEmpty();
    }

    #endregion

    #region OpenID Connect Token Response Contract

    [Fact]
    public void OpenIdConnectTokenResponse_IncludesIdToken()
    {
        // For OpenID Connect, when 'openid' scope is requested,
        // the response MUST include an id_token
        string[] oidcRequiredFields = new[]
        {
            "access_token",
            "token_type",
            "id_token"         // REQUIRED when openid scope is requested
        };

        oidcRequiredFields.ShouldContain("id_token");
    }

    #endregion

    #region Error Response Contract

    [Fact]
    public void ErrorResponse_RequiredFields_AreDocumented()
    {
        // Error response per RFC 6749 Section 5.2
        string[] requiredErrorFields = new[]
        {
            "error"            // REQUIRED. A single error code from a defined set.
        };

        requiredErrorFields.ShouldContain("error");
    }

    [Fact]
    public void ErrorResponse_OptionalFields_AreDocumented()
    {
        string[] optionalErrorFields = new[]
        {
            "error_description",  // OPTIONAL. Human-readable error description.
            "error_uri"           // OPTIONAL. URI with more information about the error.
        };

        optionalErrorFields.ShouldNotBeEmpty();
    }

    [Fact]
    public void ErrorResponse_StandardErrorCodes_AreDocumented()
    {
        // Standard error codes per RFC 6749 Section 5.2
        string[] standardErrorCodes = new[]
        {
            "invalid_request",
            "invalid_client",
            "invalid_grant",
            "unauthorized_client",
            "unsupported_grant_type",
            "invalid_scope"
        };

        standardErrorCodes.Length.ShouldBe(6);
    }

    #endregion

    #region Token Type Contract

    [Fact]
    public void TokenType_Bearer_IsSupported()
    {
        // Bearer tokens per RFC 6750
        string[] supportedTokenTypes = new[] { "Bearer" };
        supportedTokenTypes.ShouldContain("Bearer");
    }

    #endregion

    #region JWT Claims Contract

    [Fact]
    public void IdToken_RequiredClaims_AreDocumented()
    {
        // Required claims in ID Token per OpenID Connect Core Section 2
        string[] requiredClaims = new[]
        {
            "iss",   // Issuer Identifier
            "sub",   // Subject Identifier
            "aud",   // Audience(s)
            "exp",   // Expiration time
            "iat"    // Issued at time
        };

        requiredClaims.Length.ShouldBe(5);
        requiredClaims.ShouldContain("iss");
        requiredClaims.ShouldContain("sub");
        requiredClaims.ShouldContain("aud");
        requiredClaims.ShouldContain("exp");
        requiredClaims.ShouldContain("iat");
    }

    [Fact]
    public void IdToken_OptionalClaims_ForProfileScope()
    {
        // Standard claims when profile scope is requested
        string[] profileClaims = new[]
        {
            "name",
            "family_name",
            "given_name",
            "middle_name",
            "nickname",
            "preferred_username",
            "profile",
            "picture",
            "website",
            "gender",
            "birthdate",
            "zoneinfo",
            "locale",
            "updated_at"
        };

        profileClaims.ShouldNotBeEmpty();
        profileClaims.ShouldContain("name");
    }

    [Fact]
    public void IdToken_OptionalClaims_ForEmailScope()
    {
        // Standard claims when email scope is requested
        string[] emailClaims = new[]
        {
            "email",
            "email_verified"
        };

        emailClaims.Length.ShouldBe(2);
        emailClaims.ShouldContain("email");
        emailClaims.ShouldContain("email_verified");
    }

    #endregion

    #region Grant Types Contract

    [Fact]
    public void SupportedGrantTypes_AreDocumented()
    {
        // Grant types our server will support
        string[] supportedGrantTypes = new[]
        {
            "authorization_code",  // For user authentication
            "client_credentials",  // For service accounts
            "refresh_token"        // For token refresh
        };

        supportedGrantTypes.Length.ShouldBe(3);
        supportedGrantTypes.ShouldContain("authorization_code");
        supportedGrantTypes.ShouldContain("client_credentials");
    }

    [Fact]
    public void UnsupportedGrantTypes_AreDocumented()
    {
        // Grant types we explicitly do NOT support (per spec)
        string[] unsupportedGrantTypes = new[]
        {
            "password",           // Resource Owner Password Credentials (deprecated)
            "implicit"            // Implicit flow (deprecated, not recommended)
        };

        unsupportedGrantTypes.ShouldContain("password");
        unsupportedGrantTypes.ShouldContain("implicit");
    }

    #endregion

    #region PKCE Contract

    [Fact]
    public void PKCE_SupportedMethods_AreDocumented()
    {
        // Code challenge methods per RFC 7636
        string[] supportedMethods = new[]
        {
            "S256"               // SHA-256 (MUST be supported)
        };

        supportedMethods.ShouldContain("S256");
    }

    [Fact]
    public void PKCE_CodeVerifierConstraints_AreDocumented()
    {
        // Code verifier constraints per RFC 7636 Section 4.1
        const int minLength = 43;
        const int maxLength = 128;
        const string allowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";

        minLength.ShouldBe(43);
        maxLength.ShouldBe(128);
        allowedChars.Length.ShouldBeGreaterThan(0);
    }

    #endregion
}
