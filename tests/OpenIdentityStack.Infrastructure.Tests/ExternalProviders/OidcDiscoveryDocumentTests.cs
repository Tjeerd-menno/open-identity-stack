
using System.Text.Json;
using OpenIdentityStack.Infrastructure.ExternalProviders;

namespace OpenIdentityStack.Infrastructure.Tests.ExternalProviders;
/// <summary>
/// Unit tests for OidcDiscoveryDocument record.
/// </summary>
public sealed class OidcDiscoveryDocumentTests
{
    [Fact]
    public void DefaultValues_AreInitialized()
    {
        // Act
        var document = new OidcDiscoveryDocument();

        // Assert
        document.Issuer.ShouldBe(string.Empty);
        document.AuthorizationEndpoint.ShouldBe(string.Empty);
        document.TokenEndpoint.ShouldBe(string.Empty);
        document.UserInfoEndpoint.ShouldBeNull();
        document.JwksUri.ShouldBe(string.Empty);
        document.ScopesSupported.ShouldBeEmpty();
        document.ResponseTypesSupported.ShouldBeEmpty();
        document.SubjectTypesSupported.ShouldBeEmpty();
        document.IdTokenSigningAlgValuesSupported.ShouldBeEmpty();
    }

    [Fact]
    public void CanInitialize_WithValues()
    {
        // Arrange & Act
        var document = new OidcDiscoveryDocument
        {
            Issuer = "https://example.com",
            AuthorizationEndpoint = "https://example.com/authorize",
            TokenEndpoint = "https://example.com/token",
            UserInfoEndpoint = "https://example.com/userinfo",
            JwksUri = "https://example.com/.well-known/jwks.json",
            ScopesSupported = ["openid", "profile", "email"],
            ResponseTypesSupported = ["code", "token"],
            SubjectTypesSupported = ["public"],
            IdTokenSigningAlgValuesSupported = ["RS256"]
        };

        // Assert
        document.Issuer.ShouldBe("https://example.com");
        document.AuthorizationEndpoint.ShouldBe("https://example.com/authorize");
        document.TokenEndpoint.ShouldBe("https://example.com/token");
        document.UserInfoEndpoint.ShouldBe("https://example.com/userinfo");
        document.JwksUri.ShouldBe("https://example.com/.well-known/jwks.json");
        document.ScopesSupported.ShouldBe(["openid", "profile", "email"]);
        document.ResponseTypesSupported.ShouldBe(["code", "token"]);
        document.SubjectTypesSupported.ShouldBe(["public"]);
        document.IdTokenSigningAlgValuesSupported.ShouldBe(["RS256"]);
    }

    [Fact]
    public void JsonDeserialization_MapsPropertiesCorrectly()
    {
        // Arrange
        string json = """
        {
            "issuer": "https://auth.example.com",
            "authorization_endpoint": "https://auth.example.com/authorize",
            "token_endpoint": "https://auth.example.com/token",
            "userinfo_endpoint": "https://auth.example.com/userinfo",
            "jwks_uri": "https://auth.example.com/.well-known/jwks.json",
            "scopes_supported": ["openid", "profile"],
            "response_types_supported": ["code"],
            "subject_types_supported": ["public", "pairwise"],
            "id_token_signing_alg_values_supported": ["RS256", "ES256"]
        }
        """;

        // Act
        OidcDiscoveryDocument? document = JsonSerializer.Deserialize<OidcDiscoveryDocument>(json);

        // Assert
        document.ShouldNotBeNull();
        document.Issuer.ShouldBe("https://auth.example.com");
        document.AuthorizationEndpoint.ShouldBe("https://auth.example.com/authorize");
        document.TokenEndpoint.ShouldBe("https://auth.example.com/token");
        document.UserInfoEndpoint.ShouldBe("https://auth.example.com/userinfo");
        document.JwksUri.ShouldBe("https://auth.example.com/.well-known/jwks.json");
        document.ScopesSupported.ShouldBe(["openid", "profile"]);
        document.ResponseTypesSupported.ShouldBe(["code"]);
        document.SubjectTypesSupported.ShouldBe(["public", "pairwise"]);
        document.IdTokenSigningAlgValuesSupported.ShouldBe(["RS256", "ES256"]);
    }

    [Fact]
    public void JsonDeserialization_HandlesNullUserInfoEndpoint()
    {
        // Arrange
        string json = """
        {
            "issuer": "https://example.com",
            "authorization_endpoint": "https://example.com/authorize",
            "token_endpoint": "https://example.com/token",
            "jwks_uri": "https://example.com/jwks"
        }
        """;

        // Act
        OidcDiscoveryDocument? document = JsonSerializer.Deserialize<OidcDiscoveryDocument>(json);

        // Assert
        document.ShouldNotBeNull();
        document.UserInfoEndpoint.ShouldBeNull();
    }

    [Fact]
    public void JsonSerialization_ProducesCorrectPropertyNames()
    {
        // Arrange
        var document = new OidcDiscoveryDocument
        {
            Issuer = "https://example.com",
            AuthorizationEndpoint = "https://example.com/authorize",
            TokenEndpoint = "https://example.com/token",
            JwksUri = "https://example.com/jwks",
            ScopesSupported = ["openid"]
        };

        // Act
        string json = JsonSerializer.Serialize(document);

        // Assert
        json.ShouldContain("\"issuer\":");
        json.ShouldContain("\"authorization_endpoint\":");
        json.ShouldContain("\"token_endpoint\":");
        json.ShouldContain("\"jwks_uri\":");
        json.ShouldContain("\"scopes_supported\":");
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        // Arrange
        var doc1 = new OidcDiscoveryDocument
        {
            Issuer = "https://example.com",
            AuthorizationEndpoint = "https://example.com/authorize"
        };
        var doc2 = new OidcDiscoveryDocument
        {
            Issuer = "https://example.com",
            AuthorizationEndpoint = "https://example.com/authorize"
        };

        // Assert
        doc1.ShouldBe(doc2);
    }

    [Fact]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        // Arrange
        var doc1 = new OidcDiscoveryDocument { Issuer = "https://example1.com" };
        var doc2 = new OidcDiscoveryDocument { Issuer = "https://example2.com" };

        // Assert
        doc1.ShouldNotBe(doc2);
    }
}
