
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Users;
/// <summary>
/// Unit tests for the UpstreamIdentity value object.
/// </summary>
public sealed class UpstreamIdentityTests
{
    [Fact]
    public void Unlink_DoesNotEraseQuarantinedAssociationEvidence()
    {
        var providerId = UpstreamProviderId.Create();
        User user = User.CreateFederated("legacy@example.com", "Legacy", providerId, "provider", "subject", issuer: "https://issuer.example").Value;
        user.UnlinkUpstreamIdentity(providerId).IsFailure.ShouldBeTrue();
        user.UpstreamIdentities.Single().IsQuarantined.ShouldBeTrue();
    }
    [Fact]
    public void RawIdentity_RemainsQuarantinedAfterIssuerAndEmailAreSupplied()
    {
        var providerId = UpstreamProviderId.Create();
        UpstreamIdentity identity = UpstreamIdentity.Create(providerId, "provider", "subject", "mail@example.com", "https://issuer.example").Value;
        identity.RecordLogin();
        identity.AssociationEvidence.ShouldBe(IdentityAssociationEvidence.Unknown);
        identity.IsQuarantined.ShouldBeTrue();
        UpstreamIdentity updated = identity.UpdateEmail("updated@example.com").Value;
        updated.AssociationEvidence.ShouldBe(IdentityAssociationEvidence.Unknown);
        updated.IsQuarantined.ShouldBeTrue();
        updated.LinkedAt.ShouldBe(identity.LinkedAt);
    }

    [Fact]
    public void ProvisionedNewAccount_PreservesAssociationEvidenceAcrossEmailUpdates()
    {
        User user = User.ProvisionFederated("new@example.com", "New account", UpstreamProviderId.Create(), "provider", "subject", "https://issuer.example").Value;
        UpstreamIdentity identity = user.UpstreamIdentities.Single();
        identity.AssociationEvidence.ShouldBe(IdentityAssociationEvidence.NewAccountProvisioning);
        identity.IsQuarantined.ShouldBeFalse();
        UpstreamIdentity updated = identity.UpdateEmail("updated@example.com").Value;
        updated.AssociationEvidence.ShouldBe(IdentityAssociationEvidence.NewAccountProvisioning);
        updated.Issuer.ShouldBe("https://issuer.example");
        updated.IsQuarantined.ShouldBeFalse();
    }
    [Fact]
    public void Create_WithValidParameters_ReturnsUpstreamIdentity()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();
        string providerName = "azure-ad";
        string subjectId = "user-12345";
        string email = "user@example.com";

        // Act
        Result<UpstreamIdentity> result = UpstreamIdentity.Create(providerId, providerName, subjectId, email);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ProviderId.ShouldBe(providerId);
        result.Value.ProviderName.ShouldBe(providerName);
        result.Value.SubjectId.ShouldBe(subjectId);
        result.Value.Email.ShouldBe(email);
        result.Value.LinkedAt.ShouldNotBe(default);
    }

    [Fact]
    public void Create_WithEmptyProviderName_ReturnsError()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();

        // Act
        Result<UpstreamIdentity> result = UpstreamIdentity.Create(providerId, string.Empty, "subject-id", "user@example.com");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("ProviderNameRequired");
    }

    [Fact]
    public void Create_WithWhitespaceProviderName_ReturnsError()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();

        // Act
        Result<UpstreamIdentity> result = UpstreamIdentity.Create(providerId, "   ", "subject-id", "user@example.com");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("ProviderNameRequired");
    }

    [Fact]
    public void Create_WithEmptySubjectId_ReturnsError()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();

        // Act
        Result<UpstreamIdentity> result = UpstreamIdentity.Create(providerId, "azure-ad", string.Empty, "user@example.com");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("SubjectIdRequired");
    }

    [Fact]
    public void Create_WithWhitespaceSubjectId_ReturnsError()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();

        // Act
        Result<UpstreamIdentity> result = UpstreamIdentity.Create(providerId, "azure-ad", "   ", "user@example.com");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("SubjectIdRequired");
    }

    [Fact]
    public void Create_WithNullEmail_Succeeds()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();

        // Act
        Result<UpstreamIdentity> result = UpstreamIdentity.Create(providerId, "azure-ad", "subject-id", null);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBeNull();
    }

    [Fact]
    public void Create_WithEmptyEmail_Succeeds()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();

        // Act
        Result<UpstreamIdentity> result = UpstreamIdentity.Create(providerId, "azure-ad", "subject-id", string.Empty);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBeNull();
    }

    [Fact]
    public void Matches_WithSameProviderAndSubject_ReturnsTrue()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();
        UpstreamIdentity identity = UpstreamIdentity.Create(providerId, "azure-ad", "subject-id", "user@example.com").Value;

        // Act
        bool matches = identity.Matches(providerId, "subject-id");

        // Assert
        matches.ShouldBeTrue();
    }

    [Fact]
    public void Matches_WithDifferentProviderId_ReturnsFalse()
    {
        // Arrange
        var providerId1 = UpstreamProviderId.Create();
        var providerId2 = UpstreamProviderId.Create();
        UpstreamIdentity identity = UpstreamIdentity.Create(providerId1, "azure-ad", "subject-id", "user@example.com").Value;

        // Act
        bool matches = identity.Matches(providerId2, "subject-id");

        // Assert
        matches.ShouldBeFalse();
    }

    [Fact]
    public void Matches_WithDifferentSubjectId_ReturnsFalse()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();
        UpstreamIdentity identity = UpstreamIdentity.Create(providerId, "azure-ad", "subject-id", "user@example.com").Value;

        // Act
        bool matches = identity.Matches(providerId, "different-subject");

        // Assert
        matches.ShouldBeFalse();
    }

    [Fact]
    public void UpdateEmail_WithValidEmail_UpdatesEmail()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();
        UpstreamIdentity identity = UpstreamIdentity.Create(providerId, "azure-ad", "subject-id", "old@example.com").Value;
        string newEmail = "new@example.com";

        // Act
        Result<UpstreamIdentity> result = identity.UpdateEmail(newEmail);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBe(newEmail);
    }

    [Fact]
    public void UpdateEmail_WithNullEmail_ClearsEmail()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();
        UpstreamIdentity identity = UpstreamIdentity.Create(providerId, "azure-ad", "subject-id", "old@example.com").Value;

        // Act
        Result<UpstreamIdentity> result = identity.UpdateEmail(null);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBeNull();
    }

    [Fact]
    public void Equality_WithSameProviderAndSubject_AreEqual()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();
        UpstreamIdentity identity1 = UpstreamIdentity.Create(providerId, "azure-ad", "subject-id", "user1@example.com").Value;
        UpstreamIdentity identity2 = UpstreamIdentity.Create(providerId, "azure-ad", "subject-id", "user2@example.com").Value;

        // Act & Assert
        identity1.Equals(identity2).ShouldBeTrue();
    }

    [Fact]
    public void Equality_WithDifferentSubject_AreNotEqual()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();
        UpstreamIdentity identity1 = UpstreamIdentity.Create(providerId, "azure-ad", "subject-1", "user@example.com").Value;
        UpstreamIdentity identity2 = UpstreamIdentity.Create(providerId, "azure-ad", "subject-2", "user@example.com").Value;

        // Act & Assert
        identity1.Equals(identity2).ShouldBeFalse();
    }

    [Fact]
    public void Equality_WithDifferentProvider_AreNotEqual()
    {
        // Arrange
        var providerId1 = UpstreamProviderId.Create();
        var providerId2 = UpstreamProviderId.Create();
        UpstreamIdentity identity1 = UpstreamIdentity.Create(providerId1, "azure-ad", "subject-id", "user@example.com").Value;
        UpstreamIdentity identity2 = UpstreamIdentity.Create(providerId2, "google", "subject-id", "user@example.com").Value;

        // Act & Assert
        identity1.Equals(identity2).ShouldBeFalse();
    }
}
