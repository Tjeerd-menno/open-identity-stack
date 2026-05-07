
using System.Text.Json;
using OpenIdentityStack.Infrastructure.ExternalProviders;

namespace OpenIdentityStack.Infrastructure.Tests.ExternalProviders;
/// <summary>
/// Unit tests for TokenResponse and UserInfoResponse records.
/// </summary>
public sealed class ExternalProviderResponseTests
{
    public sealed class TokenResponseTests
    {
        [Fact]
        public void DefaultValues_AreInitialized()
        {
            // Act
            var response = new TokenResponse();

            // Assert
            response.AccessToken.ShouldBe(string.Empty);
            response.TokenType.ShouldBe(string.Empty);
            response.ExpiresIn.ShouldBe(0);
            response.RefreshToken.ShouldBeNull();
            response.IdToken.ShouldBeNull();
            response.Scope.ShouldBeNull();
        }

        [Fact]
        public void CanInitialize_WithValues()
        {
            // Arrange & Act
            var response = new TokenResponse
            {
                AccessToken = "access_token_123",
                TokenType = "Bearer",
                ExpiresIn = 3600,
                RefreshToken = "refresh_token_456",
                IdToken = "id_token_789",
                Scope = "openid profile email"
            };

            // Assert
            response.AccessToken.ShouldBe("access_token_123");
            response.TokenType.ShouldBe("Bearer");
            response.ExpiresIn.ShouldBe(3600);
            response.RefreshToken.ShouldBe("refresh_token_456");
            response.IdToken.ShouldBe("id_token_789");
            response.Scope.ShouldBe("openid profile email");
        }

        [Fact]
        public void JsonDeserialization_MapsPropertiesCorrectly()
        {
            // Arrange
            string json = """
            {
                "access_token": "ya29.abc123",
                "token_type": "Bearer",
                "expires_in": 3599,
                "refresh_token": "1//refresh",
                "id_token": "eyJhbGciOiJSUzI1NiJ9.eyJpc3MiOiJodHRwczovL2V4YW1wbGUuY29tIn0.signature",
                "scope": "openid email"
            }
            """;

            // Act
            TokenResponse? response = JsonSerializer.Deserialize<TokenResponse>(json);

            // Assert
            response.ShouldNotBeNull();
            response.AccessToken.ShouldBe("ya29.abc123");
            response.TokenType.ShouldBe("Bearer");
            response.ExpiresIn.ShouldBe(3599);
            response.RefreshToken.ShouldBe("1//refresh");
            response.IdToken.ShouldStartWith("eyJhbGciOiJSUzI1NiJ9");
            response.Scope.ShouldBe("openid email");
        }

        [Fact]
        public void JsonDeserialization_HandlesOptionalFields()
        {
            // Arrange - minimal response without optional fields
            string json = """
            {
                "access_token": "token123",
                "token_type": "Bearer",
                "expires_in": 3600
            }
            """;

            // Act
            TokenResponse? response = JsonSerializer.Deserialize<TokenResponse>(json);

            // Assert
            response.ShouldNotBeNull();
            response.AccessToken.ShouldBe("token123");
            response.RefreshToken.ShouldBeNull();
            response.IdToken.ShouldBeNull();
            response.Scope.ShouldBeNull();
        }

        [Fact]
        public void JsonSerialization_ProducesCorrectPropertyNames()
        {
            // Arrange
            var response = new TokenResponse
            {
                AccessToken = "test",
                TokenType = "Bearer",
                ExpiresIn = 3600
            };

            // Act
            string json = JsonSerializer.Serialize(response);

            // Assert
            json.ShouldContain("\"access_token\":");
            json.ShouldContain("\"token_type\":");
            json.ShouldContain("\"expires_in\":");
        }

        [Fact]
        public void Equals_SameValues_ReturnsTrue()
        {
            // Arrange
            var response1 = new TokenResponse
            {
                AccessToken = "token",
                TokenType = "Bearer",
                ExpiresIn = 3600
            };
            var response2 = new TokenResponse
            {
                AccessToken = "token",
                TokenType = "Bearer",
                ExpiresIn = 3600
            };

            // Assert
            response1.ShouldBe(response2);
        }

        [Fact]
        public void Equals_DifferentAccessToken_ReturnsFalse()
        {
            // Arrange
            var response1 = new TokenResponse { AccessToken = "token1" };
            var response2 = new TokenResponse { AccessToken = "token2" };

            // Assert
            response1.ShouldNotBe(response2);
        }
    }

    public sealed class UserInfoResponseTests
    {
        [Fact]
        public void DefaultValues_AreInitialized()
        {
            // Act
            var response = new UserInfoResponse();

            // Assert
            response.Sub.ShouldBe(string.Empty);
            response.Email.ShouldBeNull();
            response.EmailVerified.ShouldBeFalse();
            response.Name.ShouldBeNull();
            response.GivenName.ShouldBeNull();
            response.FamilyName.ShouldBeNull();
            response.PreferredUsername.ShouldBeNull();
            response.Picture.ShouldBeNull();
        }

        [Fact]
        public void CanInitialize_WithValues()
        {
            // Arrange & Act
            var response = new UserInfoResponse
            {
                Sub = "user-123",
                Email = "user@example.com",
                EmailVerified = true,
                Name = "John Doe",
                GivenName = "John",
                FamilyName = "Doe",
                PreferredUsername = "johnd",
                Picture = "https://example.com/photo.jpg"
            };

            // Assert
            response.Sub.ShouldBe("user-123");
            response.Email.ShouldBe("user@example.com");
            response.EmailVerified.ShouldBeTrue();
            response.Name.ShouldBe("John Doe");
            response.GivenName.ShouldBe("John");
            response.FamilyName.ShouldBe("Doe");
            response.PreferredUsername.ShouldBe("johnd");
            response.Picture.ShouldBe("https://example.com/photo.jpg");
        }

        [Fact]
        public void JsonDeserialization_MapsPropertiesCorrectly()
        {
            // Arrange
            string json = """
            {
                "sub": "auth0|123456",
                "email": "test@example.com",
                "email_verified": true,
                "name": "Test User",
                "given_name": "Test",
                "family_name": "User",
                "preferred_username": "testuser",
                "picture": "https://cdn.example.com/avatar.png"
            }
            """;

            // Act
            UserInfoResponse? response = JsonSerializer.Deserialize<UserInfoResponse>(json);

            // Assert
            response.ShouldNotBeNull();
            response.Sub.ShouldBe("auth0|123456");
            response.Email.ShouldBe("test@example.com");
            response.EmailVerified.ShouldBeTrue();
            response.Name.ShouldBe("Test User");
            response.GivenName.ShouldBe("Test");
            response.FamilyName.ShouldBe("User");
            response.PreferredUsername.ShouldBe("testuser");
            response.Picture.ShouldBe("https://cdn.example.com/avatar.png");
        }

        [Fact]
        public void JsonDeserialization_HandlesMinimalResponse()
        {
            // Arrange - minimal response with only required field
            string json = """
            {
                "sub": "user-456"
            }
            """;

            // Act
            UserInfoResponse? response = JsonSerializer.Deserialize<UserInfoResponse>(json);

            // Assert
            response.ShouldNotBeNull();
            response.Sub.ShouldBe("user-456");
            response.Email.ShouldBeNull();
            response.EmailVerified.ShouldBeFalse();
            response.Name.ShouldBeNull();
        }

        [Fact]
        public void JsonDeserialization_HandlesEmailVerifiedFalse()
        {
            // Arrange
            string json = """
            {
                "sub": "user-789",
                "email": "unverified@example.com",
                "email_verified": false
            }
            """;

            // Act
            UserInfoResponse? response = JsonSerializer.Deserialize<UserInfoResponse>(json);

            // Assert
            response.ShouldNotBeNull();
            response.Email.ShouldBe("unverified@example.com");
            response.EmailVerified.ShouldBeFalse();
        }

        [Fact]
        public void JsonSerialization_ProducesCorrectPropertyNames()
        {
            // Arrange
            var response = new UserInfoResponse
            {
                Sub = "test",
                Email = "test@test.com",
                EmailVerified = true
            };

            // Act
            string json = JsonSerializer.Serialize(response);

            // Assert
            json.ShouldContain("\"sub\":");
            json.ShouldContain("\"email\":");
            json.ShouldContain("\"email_verified\":");
        }

        [Fact]
        public void Equals_SameValues_ReturnsTrue()
        {
            // Arrange
            var response1 = new UserInfoResponse
            {
                Sub = "user-1",
                Email = "user@test.com"
            };
            var response2 = new UserInfoResponse
            {
                Sub = "user-1",
                Email = "user@test.com"
            };

            // Assert
            response1.ShouldBe(response2);
        }

        [Fact]
        public void Equals_DifferentSub_ReturnsFalse()
        {
            // Arrange
            var response1 = new UserInfoResponse { Sub = "user-1" };
            var response2 = new UserInfoResponse { Sub = "user-2" };

            // Assert
            response1.ShouldNotBe(response2);
        }
    }
}
