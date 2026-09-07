
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Federation;
/// <summary>
/// Unit tests for the UpstreamProvider aggregate root.
/// </summary>
public sealed class UpstreamProviderTests
{
    private const string ValidName = "Azure AD";
    private const string ValidDisplayName = "Azure Active Directory";
    private const string ValidAuthority = "https://login.microsoftonline.com/tenant-id/v2.0";
    private const string ValidClientId = "client-id-123";

    [Fact]
    public void BindIssuer_RejectsDifferentIssuerAndStaleDiscoveryAuthority()
    {
        UpstreamProvider provider = UpstreamProvider.Create(ValidName, ValidDisplayName, ValidAuthority, ValidClientId).Value;
        provider.BindIssuer("https://issuer.example/tenant/", ValidAuthority).IsSuccess.ShouldBeTrue();
        provider.BindIssuer("https://issuer.example/tenant", ValidAuthority).IsFailure.ShouldBeTrue();
        provider.BindIssuer("https://issuer.example/tenant/", "https://other.example").IsFailure.ShouldBeTrue();
        provider.UpdateAuthority(ValidAuthority).IsSuccess.ShouldBeTrue();
        provider.UpdateAuthority("https://replacement.example").IsFailure.ShouldBeTrue();
        provider.UpdateClientId("rotated-client").IsSuccess.ShouldBeTrue();
    }
    [Fact]
    public void Create_WithValidParameters_ReturnsProvider()
    {
        // Act
        Result<UpstreamProvider> result = UpstreamProvider.Create(
            ValidName,
            ValidDisplayName,
            ValidAuthority,
            ValidClientId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("azure ad"); // Name is normalized to lowercase
        result.Value.DisplayName.ShouldBe(ValidDisplayName);
        result.Value.Authority.ShouldBe(ValidAuthority);
        result.Value.ClientId.ShouldBe(ValidClientId);
        result.Value.Status.ShouldBe(ProviderStatus.Active);
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void SetEmailVerificationTrust_WithCurrentValue_PreservesPolicyVersion()
    {
        UpstreamProvider provider = CreateValidProvider();
        Guid version = provider.EmailTrustVersion;

        provider.SetEmailVerificationTrust(false);

        provider.TrustEmailVerification.ShouldBeFalse();
        provider.EmailTrustVersion.ShouldBe(version);
    }

    [Fact]
    public void Create_WithEmptyName_ReturnsError()
    {
        // Act
        Result<UpstreamProvider> result = UpstreamProvider.Create(
            string.Empty,
            ValidDisplayName,
            ValidAuthority,
            ValidClientId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("NameRequired");
    }

    [Fact]
    public void Create_WithWhitespaceName_ReturnsError()
    {
        // Act
        Result<UpstreamProvider> result = UpstreamProvider.Create(
            "   ",
            ValidDisplayName,
            ValidAuthority,
            ValidClientId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("NameRequired");
    }

    [Fact]
    public void Create_WithEmptyAuthority_ReturnsError()
    {
        // Act
        Result<UpstreamProvider> result = UpstreamProvider.Create(
            ValidName,
            ValidDisplayName,
            string.Empty,
            ValidClientId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("AuthorityRequired");
    }

    [Fact]
    public void Create_WithInvalidAuthorityUrl_ReturnsError()
    {
        // Act
        Result<UpstreamProvider> result = UpstreamProvider.Create(
            ValidName,
            ValidDisplayName,
            "not-a-valid-url",
            ValidClientId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("AuthorityInvalidUrl");
    }

    [Fact]
    public void Create_WithEmptyClientId_ReturnsError()
    {
        // Act
        Result<UpstreamProvider> result = UpstreamProvider.Create(
            ValidName,
            ValidDisplayName,
            ValidAuthority,
            string.Empty);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("ClientIdRequired");
    }

    [Fact]
    public void SetClientSecret_WithValidSecret_UpdatesSecret()
    {
        // Arrange
        UpstreamProvider provider = CreateValidProvider();
        string secret = "super-secret-value";

        // Act
        Result result = provider.SetClientSecret(secret);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        provider.ClientSecret.ShouldBe(secret);
    }

    [Fact]
    public void SetClientSecret_WithEmptySecret_ClearsSecret()
    {
        // Arrange
        UpstreamProvider provider = CreateValidProvider();
        provider.SetClientSecret("some-secret");

        // Act
        Result result = provider.SetClientSecret(null);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        provider.ClientSecret.ShouldBeNull();
    }

    [Fact]
    public void Disable_WithActiveProvider_DisablesProvider()
    {
        // Arrange
        UpstreamProvider provider = CreateValidProvider();

        // Act
        Result result = provider.Disable();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        provider.Status.ShouldBe(ProviderStatus.Disabled);
        provider.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Disable_WithAlreadyDisabledProvider_ReturnsError()
    {
        // Arrange
        UpstreamProvider provider = CreateValidProvider();
        provider.Disable();

        // Act
        Result result = provider.Disable();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("AlreadyDisabled");
    }

    [Fact]
    public void Enable_WithDisabledProvider_EnablesProvider()
    {
        // Arrange
        UpstreamProvider provider = CreateValidProvider();
        provider.Disable();

        // Act
        Result result = provider.Enable();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        provider.Status.ShouldBe(ProviderStatus.Active);
        provider.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Enable_WithActiveProvider_ReturnsError()
    {
        // Arrange
        UpstreamProvider provider = CreateValidProvider();

        // Act
        Result result = provider.Enable();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("AlreadyActive");
    }

    [Fact]
    public void AddClaimMapping_WithValidMapping_AddsMapping()
    {
        // Arrange
        UpstreamProvider provider = CreateValidProvider();
        Result<ClaimMapping> mapping = ClaimMapping.Create("email", "preferred_email", TransformType.Direct);

        // Act
        Result result = provider.AddClaimMapping(mapping.Value);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        provider.ClaimMappings.ShouldContain(mapping.Value);
    }

    [Fact]
    public void AddClaimMapping_WithDuplicateSourceClaim_ReturnsError()
    {
        // Arrange
        UpstreamProvider provider = CreateValidProvider();
        Result<ClaimMapping> mapping1 = ClaimMapping.Create("email", "preferred_email", TransformType.Direct);
        Result<ClaimMapping> mapping2 = ClaimMapping.Create("email", "other_email", TransformType.Direct);
        provider.AddClaimMapping(mapping1.Value);

        // Act
        Result result = provider.AddClaimMapping(mapping2.Value);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("DuplicateClaimMapping");
    }

    [Fact]
    public void RemoveClaimMapping_WithExistingMapping_RemovesMapping()
    {
        // Arrange
        UpstreamProvider provider = CreateValidProvider();
        Result<ClaimMapping> mapping = ClaimMapping.Create("email", "preferred_email", TransformType.Direct);
        provider.AddClaimMapping(mapping.Value);

        // Act
        Result result = provider.RemoveClaimMapping("email");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        provider.ClaimMappings.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveClaimMapping_WithNonExistentMapping_ReturnsError()
    {
        // Arrange
        UpstreamProvider provider = CreateValidProvider();

        // Act
        Result result = provider.RemoveClaimMapping("nonexistent");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("ClaimMappingNotFound");
    }

    [Fact]
    public void UpdateDisplayName_WithValidName_UpdatesName()
    {
        // Arrange
        UpstreamProvider provider = CreateValidProvider();
        string newDisplayName = "Updated Display Name";

        // Act
        Result result = provider.UpdateDisplayName(newDisplayName);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        provider.DisplayName.ShouldBe(newDisplayName);
    }

    [Fact]
    public void UpdateAuthority_WithValidAuthority_UpdatesAuthority()
    {
        // Arrange
        UpstreamProvider provider = CreateValidProvider();
        string newAuthority = "https://new-authority.example.com";

        // Act
        Result result = provider.UpdateAuthority(newAuthority);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        provider.Authority.ShouldBe(newAuthority);
    }

    [Fact]
    public void UpdateClientId_WithValidClientId_UpdatesClientId()
    {
        // Arrange
        UpstreamProvider provider = CreateValidProvider();
        string newClientId = "new-client-id-456";

        // Act
        Result result = provider.UpdateClientId(newClientId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        provider.ClientId.ShouldBe(newClientId);
    }

    private static UpstreamProvider CreateValidProvider()
    {
        return UpstreamProvider.Create(
            ValidName,
            ValidDisplayName,
            ValidAuthority,
            ValidClientId).Value;
    }
}
