using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Clients;

/// <summary>
/// Tests for Client entity creation and validation.
/// </summary>
public sealed class ClientTests
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly DateTimeOffset _now = new(2026, 1, 30, 12, 0, 0, TimeSpan.Zero);

    public ClientTests()
    {
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(this._now);
    }

    #region Create Tests

    [Fact]
    public void Create_WithValidConfidentialClient_ReturnsSuccessWithClient()
    {
        // Arrange
        string clientId = "my-confidential-client";
        string displayName = "My Confidential Client";
        var redirectUris = new List<string> { "https://example.com/callback" };
        var postLogoutRedirectUris = new List<string> { "https://example.com/logout" };
        var allowedScopes = new List<string> { "openid", "profile", "api" };
        var allowedGrantTypes = new List<string> { "authorization_code", "refresh_token" };

        // Act
        Result<Client> result = Client.Create(
            clientId,
            displayName,
            ClientType.Confidential,
            "Test confidential client",
            redirectUris,
            postLogoutRedirectUris,
            allowedScopes,
            allowedGrantTypes,
            requirePkce: true,
            requireConsent: true,
            this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ClientIdValue.ShouldBe(clientId);
        result.Value.DisplayName.ShouldBe(displayName);
        result.Value.ClientType.ShouldBe(ClientType.Confidential);
        result.Value.Description.ShouldBe("Test confidential client");
        result.Value.RedirectUris.ShouldBe(redirectUris);
        result.Value.PostLogoutRedirectUris.ShouldBe(postLogoutRedirectUris);
        result.Value.AllowedScopes.ShouldBe(allowedScopes);
        result.Value.AllowedGrantTypes.ShouldBe(allowedGrantTypes);
        result.Value.RequirePkce.ShouldBeTrue();
        result.Value.RequireConsent.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithValidPublicClient_ReturnsSuccessWithClient()
    {
        // Arrange
        string clientId = "my-public-client";
        string displayName = "My Public Client";
        var redirectUris = new List<string> { "https://spa.example.com/callback" };
        var allowedScopes = new List<string> { "openid", "profile" };
        var allowedGrantTypes = new List<string> { "authorization_code" };

        // Act
        Result<Client> result = Client.Create(
            clientId,
            displayName,
            ClientType.Public,
            null,
            redirectUris,
            new List<string>(),
            allowedScopes,
            allowedGrantTypes,
            requirePkce: true,
            requireConsent: false,
            this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ClientIdValue.ShouldBe(clientId);
        result.Value.ClientType.ShouldBe(ClientType.Public);
        result.Value.RequirePkce.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithValidData_SetsCreatedAt()
    {
        // Act
        Result<Client> result = Client.Create(
            "client-id",
            "Display Name",
            ClientType.Confidential,
            null,
            new List<string> { "https://example.com/callback" },
            new List<string>(),
            new List<string> { "api" },
            new List<string> { "authorization_code" },
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.CreatedAt.ShouldBe(this._now);
    }

    [Fact]
    public void Create_WithValidData_SetsModifiedAt()
    {
        // Act
        Result<Client> result = Client.Create(
            "client-id",
            "Display Name",
            ClientType.Confidential,
            null,
            new List<string> { "https://example.com/callback" },
            new List<string>(),
            new List<string> { "api" },
            new List<string> { "authorization_code" },
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ModifiedAt.ShouldBe(this._now);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Create_WithNullClientId_ReturnsClientIdRequiredError()
    {
        // Act
        Result<Client> result = Client.Create(
            null!,
            "Display Name",
            ClientType.Confidential,
            null,
            new List<string>(),
            new List<string>(),
            new List<string> { "api" },
            new List<string> { "client_credentials" },
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Client.ClientIdRequired");
    }

    [Fact]
    public void Create_WithEmptyClientId_ReturnsClientIdRequiredError()
    {
        // Act
        Result<Client> result = Client.Create(
            string.Empty,
            "Display Name",
            ClientType.Confidential,
            null,
            new List<string>(),
            new List<string>(),
            new List<string> { "api" },
            new List<string> { "client_credentials" },
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Client.ClientIdRequired");
    }

    [Fact]
    public void Create_WithClientIdTooLong_ReturnsClientIdTooLongError()
    {
        // Arrange
        string longClientId = new('a', 256);

        // Act
        Result<Client> result = Client.Create(
            longClientId,
            "Display Name",
            ClientType.Confidential,
            null,
            new List<string>(),
            new List<string>(),
            new List<string> { "api" },
            new List<string> { "client_credentials" },
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Client.ClientIdTooLong");
    }

    [Fact]
    public void Create_WithNullDisplayName_ReturnsDisplayNameRequiredError()
    {
        // Act
        Result<Client> result = Client.Create(
            "client-id",
            null!,
            ClientType.Confidential,
            null,
            new List<string>(),
            new List<string>(),
            new List<string> { "api" },
            new List<string> { "client_credentials" },
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Client.DisplayNameRequired");
    }

    [Fact]
    public void Create_WithEmptyDisplayName_ReturnsDisplayNameRequiredError()
    {
        // Act
        Result<Client> result = Client.Create(
            "client-id",
            string.Empty,
            ClientType.Confidential,
            null,
            new List<string>(),
            new List<string>(),
            new List<string> { "api" },
            new List<string> { "client_credentials" },
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Client.DisplayNameRequired");
    }

    [Fact]
    public void Create_WithDisplayNameTooLong_ReturnsDisplayNameTooLongError()
    {
        // Arrange
        string longDisplayName = new('a', 256);

        // Act
        Result<Client> result = Client.Create(
            "client-id",
            longDisplayName,
            ClientType.Confidential,
            null,
            new List<string>(),
            new List<string>(),
            new List<string> { "api" },
            new List<string> { "client_credentials" },
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Client.DisplayNameTooLong");
    }

    [Fact]
    public void Create_WithInvalidRedirectUri_ReturnsInvalidRedirectUriError()
    {
        // Arrange
        var invalidRedirectUris = new List<string> { "not-a-valid-uri" };

        // Act
        Result<Client> result = Client.Create(
            "client-id",
            "Display Name",
            ClientType.Confidential,
            null,
            invalidRedirectUris,
            new List<string>(),
            new List<string> { "api" },
            new List<string> { "authorization_code" },
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Client.InvalidRedirectUri");
    }

    [Fact]
    public void Create_WithInvalidPostLogoutRedirectUri_ReturnsInvalidRedirectUriError()
    {
        // Arrange
        var invalidPostLogoutUris = new List<string> { "not-a-valid-uri" };

        // Act
        Result<Client> result = Client.Create(
            "client-id",
            "Display Name",
            ClientType.Confidential,
            null,
            new List<string> { "https://example.com/callback" },
            invalidPostLogoutUris,
            new List<string> { "api" },
            new List<string> { "authorization_code" },
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Client.InvalidRedirectUri");
    }

    [Fact]
    public void Create_WithAuthorizationCodeFlowButNoRedirectUris_ReturnsRedirectUriRequiredError()
    {
        // Act
        Result<Client> result = Client.Create(
            "client-id",
            "Display Name",
            ClientType.Confidential,
            null,
            new List<string>(), // No redirect URIs
            new List<string>(),
            new List<string> { "api" },
            new List<string> { "authorization_code" }, // But using authorization_code flow
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Client.RedirectUriRequired");
    }

    [Fact]
    public void Create_WithEmptyScope_ReturnsInvalidScopeError()
    {
        // Arrange
        var scopesWithEmpty = new List<string> { "api", "" };

        // Act
        Result<Client> result = Client.Create(
            "client-id",
            "Display Name",
            ClientType.Confidential,
            null,
            new List<string>(),
            new List<string>(),
            scopesWithEmpty,
            new List<string> { "client_credentials" },
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Client.InvalidScope");
    }

    [Fact]
    public void Create_WithEmptyGrantType_ReturnsInvalidGrantTypeError()
    {
        // Arrange
        var grantTypesWithEmpty = new List<string> { "client_credentials", "" };

        // Act
        Result<Client> result = Client.Create(
            "client-id",
            "Display Name",
            ClientType.Confidential,
            null,
            new List<string>(),
            new List<string>(),
            new List<string> { "api" },
            grantTypesWithEmpty,
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Client.InvalidGrantType");
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_WithValidData_ReturnsSuccess()
    {
        // Arrange
        Result<Client> createResult = Client.Create(
            "client-id",
            "Original Name",
            ClientType.Confidential,
            "Original description",
            new List<string> { "https://example.com/callback" },
            new List<string>(),
            new List<string> { "api" },
            new List<string> { "authorization_code" },
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);
        createResult.IsSuccess.ShouldBeTrue();
        Client client = createResult.Value;

        // Act
        Result updateResult = client.Update(
            "Updated Name",
            "Updated description",
            this._dateTimeProvider);

        // Assert
        updateResult.IsSuccess.ShouldBeTrue();
        client.DisplayName.ShouldBe("Updated Name");
        client.Description.ShouldBe("Updated description");
    }

    [Fact]
    public void Update_WithNullDisplayName_ReturnsDisplayNameRequiredError()
    {
        // Arrange
        Result<Client> createResult = Client.Create(
            "client-id",
            "Original Name",
            ClientType.Confidential,
            null,
            new List<string> { "https://example.com/callback" },
            new List<string>(),
            new List<string> { "api" },
            new List<string> { "authorization_code" },
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);
        createResult.IsSuccess.ShouldBeTrue();
        Client client = createResult.Value;

        // Act
        Result updateResult = client.Update(
            null!,
            "Updated description",
            this._dateTimeProvider);

        // Assert
        updateResult.IsFailure.ShouldBeTrue();
        updateResult.Error.Code.ShouldBe("Validation.Client.DisplayNameRequired");
    }

    [Fact]
    public void UpdateRedirectUris_WithValidUris_ReturnsSuccess()
    {
        // Arrange
        Result<Client> createResult = Client.Create(
            "client-id",
            "Name",
            ClientType.Confidential,
            null,
            new List<string> { "https://example.com/callback" },
            new List<string>(),
            new List<string> { "api" },
            new List<string> { "authorization_code" },
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);
        createResult.IsSuccess.ShouldBeTrue();
        Client client = createResult.Value;

        var newRedirectUris = new List<string> 
        { 
            "https://example.com/callback",
            "https://example.com/callback2" 
        };
        var newPostLogoutUris = new List<string> 
        { 
            "https://example.com/logout" 
        };

        // Act
        Result updateResult = client.UpdateRedirectUris(
            newRedirectUris,
            newPostLogoutUris,
            this._dateTimeProvider);

        // Assert
        updateResult.IsSuccess.ShouldBeTrue();
        client.RedirectUris.ShouldBe(newRedirectUris);
        client.PostLogoutRedirectUris.ShouldBe(newPostLogoutUris);
    }

    [Fact]
    public void UpdateRedirectUris_WithInvalidUri_ReturnsInvalidRedirectUriError()
    {
        // Arrange
        Result<Client> createResult = Client.Create(
            "client-id",
            "Name",
            ClientType.Confidential,
            null,
            new List<string> { "https://example.com/callback" },
            new List<string>(),
            new List<string> { "api" },
            new List<string> { "authorization_code" },
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);
        createResult.IsSuccess.ShouldBeTrue();
        Client client = createResult.Value;

        var invalidRedirectUris = new List<string> { "not-a-uri" };

        // Act
        Result updateResult = client.UpdateRedirectUris(
            invalidRedirectUris,
            new List<string>(),
            this._dateTimeProvider);

        // Assert
        updateResult.IsFailure.ShouldBeTrue();
        updateResult.Error.Code.ShouldBe("Validation.Client.InvalidRedirectUri");
    }

    [Fact]
    public void UpdatePermissions_WithValidData_ReturnsSuccess()
    {
        // Arrange
        Result<Client> createResult = Client.Create(
            "client-id",
            "Name",
            ClientType.Confidential,
            null,
            new List<string> { "https://example.com/callback" },
            new List<string>(),
            new List<string> { "api" },
            new List<string> { "authorization_code" },
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);
        createResult.IsSuccess.ShouldBeTrue();
        Client client = createResult.Value;

        var newScopes = new List<string> { "api", "openid", "profile" };
        var newGrantTypes = new List<string> { "authorization_code", "refresh_token" };

        // Act
        Result updateResult = client.UpdatePermissions(
            newScopes,
            newGrantTypes,
            requirePkce: true,
            requireConsent: true,
            this._dateTimeProvider);

        // Assert
        updateResult.IsSuccess.ShouldBeTrue();
        client.AllowedScopes.ShouldBe(newScopes);
        client.AllowedGrantTypes.ShouldBe(newGrantTypes);
        client.RequirePkce.ShouldBeTrue();
        client.RequireConsent.ShouldBeTrue();
    }

    [Fact]
    public void UpdatePermissions_WithInvalidScope_ReturnsInvalidScopeError()
    {
        // Arrange
        Result<Client> createResult = Client.Create(
            "client-id",
            "Name",
            ClientType.Confidential,
            null,
            new List<string>(),
            new List<string>(),
            new List<string> { "api" },
            new List<string> { "client_credentials" },
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);
        createResult.IsSuccess.ShouldBeTrue();
        Client client = createResult.Value;

        var invalidScopes = new List<string> { "api", "" };

        // Act
        Result updateResult = client.UpdatePermissions(
            invalidScopes,
            new List<string> { "client_credentials" },
            requirePkce: false,
            requireConsent: false,
            this._dateTimeProvider);

        // Assert
        updateResult.IsFailure.ShouldBeTrue();
        updateResult.Error.Code.ShouldBe("Validation.Client.InvalidScope");
    }

    #endregion
}
