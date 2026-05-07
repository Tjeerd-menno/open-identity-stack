using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.ServiceAccounts;

/// <summary>
/// Tests for ServiceAccount entity creation and validation.
/// </summary>
public sealed class ServiceAccountTests
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly DateTimeOffset _now = new(2026, 1, 18, 12, 0, 0, TimeSpan.Zero);

    public ServiceAccountTests()
    {
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(this._now);
    }

    #region Create Tests

    [Fact]
    public void Create_WithValidData_ReturnsSuccessWithServiceAccount()
    {
        // Arrange
        string clientId = "my-service-client";
        string displayName = "My Service";
        string[] allowedScopes = new[] { "api:read", "api:write" };
        string[] allowedGrantTypes = new[] { "client_credentials" };

        // Act
        Result<ServiceAccount> result = ServiceAccount.Create(
            clientId,
            displayName,
            allowedScopes,
            allowedGrantTypes,
            this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ClientId.ShouldBe(clientId);
        result.Value.DisplayName.ShouldBe(displayName);
        result.Value.AllowedScopes.ShouldBe(allowedScopes);
        result.Value.AllowedGrantTypes.ShouldBe(allowedGrantTypes);
        result.Value.Status.ShouldBe(ServiceAccountStatus.Active);
    }

    [Fact]
    public void Create_WithValidData_SetsCreatedAt()
    {
        // Act
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.CreatedAt.ShouldBe(this._now);
    }

    [Fact]
    public void Create_WithValidData_SetsModifiedAt()
    {
        // Act
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ModifiedAt.ShouldBe(this._now);
    }

    [Fact]
    public void Create_WithValidData_RaisesServiceAccountCreatedEvent()
    {
        // Act
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        IReadOnlyList<DomainEvent> domainEvents = result.Value.GetDomainEvents();
        domainEvents.ShouldContain(e => e is ServiceAccountDomainEvents.ServiceAccountCreated);

        ServiceAccountDomainEvents.ServiceAccountCreated createdEvent = domainEvents.OfType<ServiceAccountDomainEvents.ServiceAccountCreated>().Single();
        createdEvent.ServiceAccountId.ShouldBe(result.Value.Id);
        createdEvent.ClientId.ShouldBe("my-client");
    }

    [Fact]
    public void Create_WithValidData_GeneratesUniqueId()
    {
        // Act
        Result<ServiceAccount> result1 = ServiceAccount.Create(
            "client-1",
            "Service 1",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        Result<ServiceAccount> result2 = ServiceAccount.Create(
            "client-2",
            "Service 2",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);

        // Assert
        result1.Value.Id.ShouldNotBe(result2.Value.Id);
    }

    [Fact]
    public void Create_WithValidData_HasNoCredentialsInitially()
    {
        // Act
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Credentials.ShouldBeEmpty();
        result.Value.Certificates.ShouldBeEmpty();
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyClientId_ReturnsFailure(string? clientId)
    {
        // Act
        Result<ServiceAccount> result = ServiceAccount.Create(
            clientId!,
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ServiceAccount.ClientIdRequired");
    }

    [Fact]
    public void Create_WithTooLongClientId_ReturnsFailure()
    {
        // Arrange
        string clientId = new string('a', 129); // Max is 128

        // Act
        Result<ServiceAccount> result = ServiceAccount.Create(
            clientId,
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ServiceAccount.ClientIdTooLong");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyDisplayName_ReturnsFailure(string? displayName)
    {
        // Act
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            displayName!,
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ServiceAccount.DisplayNameRequired");
    }

    [Fact]
    public void Create_WithTooLongDisplayName_ReturnsFailure()
    {
        // Arrange
        string displayName = new string('a', 257); // Max is 256

        // Act
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            displayName,
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ServiceAccount.DisplayNameTooLong");
    }

    [Fact]
    public void Create_WithEmptyAllowedScopes_ReturnsFailure()
    {
        // Act
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            [],
            ["client_credentials"],
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ServiceAccount.ScopesRequired");
    }

    [Fact]
    public void Create_WithEmptyAllowedGrantTypes_ReturnsFailure()
    {
        // Act
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            [],
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ServiceAccount.GrantTypesRequired");
    }

    [Fact]
    public void Create_WithInvalidGrantType_ReturnsFailure()
    {
        // Act
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["invalid_grant"],
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ServiceAccount.InvalidGrantType");
    }

    [Theory]
    [InlineData("client_credentials")]
    [InlineData("authorization_code")]
    [InlineData("refresh_token")]
    public void Create_WithValidGrantType_ReturnsSuccess(string grantType)
    {
        // Act
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            [grantType],
            this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    #endregion

    #region Status Transition Tests

    [Fact]
    public void Disable_WhenActive_SetsStatusToDisabled()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;

        // Act
        Result disableResult = account.Disable(this._dateTimeProvider);

        // Assert
        disableResult.IsSuccess.ShouldBeTrue();
        account.Status.ShouldBe(ServiceAccountStatus.Disabled);
    }

    [Fact]
    public void Disable_WhenActive_UpdatesModifiedAt()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;

        DateTimeOffset laterTime = this._now.AddHours(1);
        this._dateTimeProvider.UtcNow.Returns(laterTime);

        // Act
        account.Disable(this._dateTimeProvider);

        // Assert
        account.ModifiedAt.ShouldBe(laterTime);
    }

    [Fact]
    public void Disable_WhenActive_RaisesServiceAccountDisabledEvent()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;
        account.ClearDomainEvents();

        // Act
        account.Disable(this._dateTimeProvider);

        // Assert
        IReadOnlyList<DomainEvent> domainEvents = account.GetDomainEvents();
        domainEvents.ShouldContain(e => e is ServiceAccountDomainEvents.ServiceAccountDisabled);
    }

    [Fact]
    public void Disable_WhenAlreadyDisabled_ReturnsFailure()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;
        account.Disable(this._dateTimeProvider);

        // Act
        Result disableResult = account.Disable(this._dateTimeProvider);

        // Assert
        disableResult.IsFailure.ShouldBeTrue();
        disableResult.Error.Code.ShouldBe("ServiceAccount.AlreadyDisabled");
    }

    [Fact]
    public void Enable_WhenDisabled_SetsStatusToActive()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;
        account.Disable(this._dateTimeProvider);

        // Act
        Result enableResult = account.Enable(this._dateTimeProvider);

        // Assert
        enableResult.IsSuccess.ShouldBeTrue();
        account.Status.ShouldBe(ServiceAccountStatus.Active);
    }

    [Fact]
    public void Enable_WhenAlreadyActive_ReturnsFailure()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;

        // Act
        Result enableResult = account.Enable(this._dateTimeProvider);

        // Assert
        enableResult.IsFailure.ShouldBeTrue();
        enableResult.Error.Code.ShouldBe("ServiceAccount.AlreadyActive");
    }

    #endregion

    #region Credential Management Tests

    [Fact]
    public void AddCredential_WithValidData_AddsCredentialToList()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;

        // Act
        Result<Guid> addResult = account.AddCredential(
            "hashed_secret",
            "API Key for CI/CD",
            expiresAt: null,
            this._dateTimeProvider);

        // Assert
        addResult.IsSuccess.ShouldBeTrue();
        account.Credentials.ShouldHaveSingleItem();
        account.Credentials[0].SecretHash.ShouldBe("hashed_secret");
        account.Credentials[0].Description.ShouldBe("API Key for CI/CD");
    }

    [Fact]
    public void AddCredential_WithExpiration_SetsExpiresAt()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;
        DateTimeOffset expiresAt = this._now.AddYears(1);

        // Act
        Result<Guid> addResult = account.AddCredential(
            "hashed_secret",
            "Temporary Key",
            expiresAt,
            this._dateTimeProvider);

        // Assert
        addResult.IsSuccess.ShouldBeTrue();
        account.Credentials[0].ExpiresAt.ShouldBe(expiresAt);
    }

    [Fact]
    public void AddCredential_RaisesCredentialAddedEvent()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;
        account.ClearDomainEvents();

        // Act
        account.AddCredential("hashed_secret", "Key", null, this._dateTimeProvider);

        // Assert
        IReadOnlyList<DomainEvent> domainEvents = account.GetDomainEvents();
        domainEvents.ShouldContain(e => e is ServiceAccountDomainEvents.CredentialAdded);
    }

    [Fact]
    public void RevokeCredential_WithValidId_RevokesCredential()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;
        account.AddCredential("hashed_secret", "Key", null, this._dateTimeProvider);
        Guid credentialId = account.Credentials[0].Id;

        // Act
        Result revokeResult = account.RevokeCredential(credentialId, this._dateTimeProvider);

        // Assert
        revokeResult.IsSuccess.ShouldBeTrue();
        account.Credentials[0].IsRevoked.ShouldBeTrue();
    }

    [Fact]
    public void RevokeCredential_WithInvalidId_ReturnsFailure()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;

        // Act
        Result revokeResult = account.RevokeCredential(Guid.NewGuid(), this._dateTimeProvider);

        // Assert
        revokeResult.IsFailure.ShouldBeTrue();
        revokeResult.Error.Code.ShouldBe("ServiceAccount.CredentialNotFound");
    }

    #endregion

    #region Certificate Management Tests

    [Fact]
    public void AddCertificate_WithValidData_AddsCertificateToList()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;
        DateTimeOffset expiresAt = this._now.AddYears(1);

        // Act
        Result<Guid> addResult = account.AddCertificate(
            "ABC123DEF456",
            "CN=my-service.example.com",
            expiresAt,
            this._dateTimeProvider);

        // Assert
        addResult.IsSuccess.ShouldBeTrue();
        account.Certificates.ShouldHaveSingleItem();
        account.Certificates[0].Thumbprint.ShouldBe("ABC123DEF456");
        account.Certificates[0].Subject.ShouldBe("CN=my-service.example.com");
        account.Certificates[0].ExpiresAt.ShouldBe(expiresAt);
    }

    [Fact]
    public void AddCertificate_RaisesCertificateAddedEvent()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;
        account.ClearDomainEvents();

        // Act
        account.AddCertificate("ABC123", "CN=test", this._now.AddYears(1), this._dateTimeProvider);

        // Assert
        IReadOnlyList<DomainEvent> domainEvents = account.GetDomainEvents();
        domainEvents.ShouldContain(e => e is ServiceAccountDomainEvents.CertificateAdded);
    }

    [Fact]
    public void AddCertificate_WithDuplicateThumbprint_ReturnsFailure()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;
        account.AddCertificate("ABC123", "CN=test1", this._now.AddYears(1), this._dateTimeProvider);

        // Act
        Result<Guid> addResult = account.AddCertificate("ABC123", "CN=test2", this._now.AddYears(1), this._dateTimeProvider);

        // Assert
        addResult.IsFailure.ShouldBeTrue();
        addResult.Error.Code.ShouldBe("ServiceAccount.CertificateThumbprintExists");
    }

    [Fact]
    public void RevokeCertificate_WithValidId_RevokesCertificate()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;
        account.AddCertificate("ABC123", "CN=test", this._now.AddYears(1), this._dateTimeProvider);
        Guid certificateId = account.Certificates[0].Id;

        // Act
        Result revokeResult = account.RevokeCertificate(certificateId, this._dateTimeProvider);

        // Assert
        revokeResult.IsSuccess.ShouldBeTrue();
        account.Certificates[0].IsRevoked.ShouldBeTrue();
    }

    #endregion

    #region Update Tests

    [Fact]
    public void UpdateDisplayName_WithValidName_UpdatesName()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;

        // Act
        Result updateResult = account.UpdateDisplayName("New Name", this._dateTimeProvider);

        // Assert
        updateResult.IsSuccess.ShouldBeTrue();
        account.DisplayName.ShouldBe("New Name");
    }

    [Fact]
    public void UpdateAllowedScopes_WithValidScopes_UpdatesScopes()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;

        // Act
        Result updateResult = account.UpdateAllowedScopes(["api:read", "api:write", "api:admin"], this._dateTimeProvider);

        // Assert
        updateResult.IsSuccess.ShouldBeTrue();
        account.AllowedScopes.Count.ShouldBe(3);
        account.AllowedScopes[0].ShouldBe("api:read");
        account.AllowedScopes[1].ShouldBe("api:write");
        account.AllowedScopes[2].ShouldBe("api:admin");
    }

    #endregion

    #region Validation Helper Tests

    [Fact]
    public void HasValidCredential_WithActiveUnexpiredCredential_ReturnsTrue()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;
        account.AddCredential("hashed_secret", "Key", null, this._dateTimeProvider);

        // Act
        bool hasValid = account.HasValidCredential(this._dateTimeProvider);

        // Assert
        hasValid.ShouldBeTrue();
    }

    [Fact]
    public void HasValidCredential_WithNoCredentials_ReturnsFalse()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;

        // Act
        bool hasValid = account.HasValidCredential(this._dateTimeProvider);

        // Assert
        hasValid.ShouldBeFalse();
    }

    [Fact]
    public void HasValidCredential_WithExpiredCredential_ReturnsFalse()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;
        account.AddCredential("hashed_secret", "Key", this._now.AddDays(-1), this._dateTimeProvider);

        // Act
        bool hasValid = account.HasValidCredential(this._dateTimeProvider);

        // Assert
        hasValid.ShouldBeFalse();
    }

    [Fact]
    public void HasValidCredential_WithRevokedCredential_ReturnsFalse()
    {
        // Arrange
        Result<ServiceAccount> result = ServiceAccount.Create(
            "my-client",
            "My Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = result.Value;
        account.AddCredential("hashed_secret", "Key", null, this._dateTimeProvider);
        account.RevokeCredential(account.Credentials[0].Id, this._dateTimeProvider);

        // Act
        bool hasValid = account.HasValidCredential(this._dateTimeProvider);

        // Assert
        hasValid.ShouldBeFalse();
    }

    #endregion
}
