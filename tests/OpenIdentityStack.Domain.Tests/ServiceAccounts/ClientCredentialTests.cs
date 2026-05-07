using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServiceAccounts;

namespace OpenIdentityStack.Domain.Tests.ServiceAccounts;

/// <summary>
/// Tests for ClientCredential value object.
/// </summary>
public sealed class ClientCredentialTests
{
    private readonly DateTimeOffset _now = new(2026, 1, 18, 12, 0, 0, TimeSpan.Zero);

    #region Creation Tests

    [Fact]
    public void Create_WithValidData_ReturnsClientCredential()
    {
        // Arrange
        var serviceAccountId = ServiceAccountId.Create();

        // Act
        var credential = ClientCredential.Create(
            serviceAccountId,
            "hashed_secret_value",
            "Production API Key",
            expiresAt: null,
            this._now);

        // Assert
        credential.Id.ShouldNotBe(Guid.Empty);
        credential.ServiceAccountId.ShouldBe(serviceAccountId);
        credential.SecretHash.ShouldBe("hashed_secret_value");
        credential.Description.ShouldBe("Production API Key");
        credential.ExpiresAt.ShouldBeNull();
        credential.CreatedAt.ShouldBe(this._now);
        credential.LastUsedAt.ShouldBeNull();
        credential.IsRevoked.ShouldBeFalse();
    }

    [Fact]
    public void Create_WithExpiration_SetsExpiresAt()
    {
        // Arrange
        DateTimeOffset expiresAt = this._now.AddYears(1);

        // Act
        var credential = ClientCredential.Create(
            ServiceAccountId.Create(),
            "hashed_secret",
            "Temporary Key",
            expiresAt,
            this._now);

        // Assert
        credential.ExpiresAt.ShouldBe(expiresAt);
    }

    [Fact]
    public void Create_WithNullDescription_SetsNullDescription()
    {
        // Act
        var credential = ClientCredential.Create(
            ServiceAccountId.Create(),
            "hashed_secret",
            description: null,
            expiresAt: null,
            this._now);

        // Assert
        credential.Description.ShouldBeNull();
    }

    #endregion

    #region Expiration Tests

    [Fact]
    public void IsExpired_WhenNoExpiration_ReturnsFalse()
    {
        // Arrange
        var credential = ClientCredential.Create(
            ServiceAccountId.Create(),
            "hashed_secret",
            null,
            expiresAt: null,
            this._now);

        // Act
        bool isExpired = credential.IsExpired(this._now.AddYears(100));

        // Assert
        isExpired.ShouldBeFalse();
    }

    [Fact]
    public void IsExpired_WhenBeforeExpiration_ReturnsFalse()
    {
        // Arrange
        DateTimeOffset expiresAt = this._now.AddDays(30);
        var credential = ClientCredential.Create(
            ServiceAccountId.Create(),
            "hashed_secret",
            null,
            expiresAt,
            this._now);

        // Act
        bool isExpired = credential.IsExpired(this._now);

        // Assert
        isExpired.ShouldBeFalse();
    }

    [Fact]
    public void IsExpired_WhenAfterExpiration_ReturnsTrue()
    {
        // Arrange
        DateTimeOffset expiresAt = this._now.AddDays(30);
        var credential = ClientCredential.Create(
            ServiceAccountId.Create(),
            "hashed_secret",
            null,
            expiresAt,
            this._now);

        // Act
        bool isExpired = credential.IsExpired(this._now.AddDays(31));

        // Assert
        isExpired.ShouldBeTrue();
    }

    [Fact]
    public void IsExpired_WhenExactlyAtExpiration_ReturnsTrue()
    {
        // Arrange
        DateTimeOffset expiresAt = this._now.AddDays(30);
        var credential = ClientCredential.Create(
            ServiceAccountId.Create(),
            "hashed_secret",
            null,
            expiresAt,
            this._now);

        // Act
        bool isExpired = credential.IsExpired(expiresAt);

        // Assert
        isExpired.ShouldBeTrue();
    }

    #endregion

    #region Validity Tests

    [Fact]
    public void IsValid_WhenNotRevokedAndNotExpired_ReturnsTrue()
    {
        // Arrange
        var credential = ClientCredential.Create(
            ServiceAccountId.Create(),
            "hashed_secret",
            null,
            expiresAt: null,
            this._now);

        // Act
        bool isValid = credential.IsValid(this._now);

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public void IsValid_WhenRevoked_ReturnsFalse()
    {
        // Arrange
        var credential = ClientCredential.Create(
            ServiceAccountId.Create(),
            "hashed_secret",
            null,
            expiresAt: null,
            this._now);
        credential = credential.Revoke();

        // Act
        bool isValid = credential.IsValid(this._now);

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WhenExpired_ReturnsFalse()
    {
        // Arrange
        var credential = ClientCredential.Create(
            ServiceAccountId.Create(),
            "hashed_secret",
            null,
            expiresAt: this._now.AddDays(-1),
            this._now);

        // Act
        bool isValid = credential.IsValid(this._now);

        // Assert
        isValid.ShouldBeFalse();
    }

    #endregion

    #region Revoke Tests

    [Fact]
    public void Revoke_SetsIsRevokedToTrue()
    {
        // Arrange
        var credential = ClientCredential.Create(
            ServiceAccountId.Create(),
            "hashed_secret",
            null,
            null,
            this._now);

        // Act
        credential = credential.Revoke();

        // Assert
        credential.IsRevoked.ShouldBeTrue();
    }

    [Fact]
    public void Revoke_ReturnsNewInstance()
    {
        // Arrange
        var original = ClientCredential.Create(
            ServiceAccountId.Create(),
            "hashed_secret",
            null,
            null,
            this._now);

        // Act
        ClientCredential revoked = original.Revoke();

        // Assert
        revoked.ShouldNotBeSameAs(original);
        original.IsRevoked.ShouldBeFalse();
        revoked.IsRevoked.ShouldBeTrue();
    }

    #endregion

    #region LastUsed Tests

    [Fact]
    public void RecordUsage_UpdatesLastUsedAt()
    {
        // Arrange
        var credential = ClientCredential.Create(
            ServiceAccountId.Create(),
            "hashed_secret",
            null,
            null,
            this._now);

        DateTimeOffset usageTime = this._now.AddHours(1);

        // Act
        credential = credential.RecordUsage(usageTime);

        // Assert
        credential.LastUsedAt.ShouldBe(usageTime);
    }

    [Fact]
    public void RecordUsage_ReturnsNewInstance()
    {
        // Arrange
        var original = ClientCredential.Create(
            ServiceAccountId.Create(),
            "hashed_secret",
            null,
            null,
            this._now);

        // Act
        ClientCredential updated = original.RecordUsage(this._now);

        // Assert
        updated.ShouldNotBeSameAs(original);
        original.LastUsedAt.ShouldBeNull();
        updated.LastUsedAt.ShouldNotBeNull();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameId_ReturnsTrue()
    {
        // Arrange
        var credential1 = ClientCredential.Create(
            ServiceAccountId.Create(),
            "hash1",
            "Desc1",
            null,
            this._now);
        
        // Create second credential with same ID by using internal constructor simulation
        var credential2 = ClientCredential.CreateForTesting(
            credential1.Id,
            credential1.ServiceAccountId,
            "hash2", // Different hash
            "Desc2", // Different description
            null,
            this._now,
            null,
            false);

        // Act & Assert
        credential1.Equals(credential2).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithDifferentId_ReturnsFalse()
    {
        // Arrange
        var serviceAccountId = ServiceAccountId.Create();
        var credential1 = ClientCredential.Create(
            serviceAccountId,
            "hash",
            "Desc",
            null,
            this._now);
        var credential2 = ClientCredential.Create(
            serviceAccountId,
            "hash",
            "Desc",
            null,
            this._now);

        // Act & Assert
        credential1.Equals(credential2).ShouldBeFalse();
    }

    #endregion
}
