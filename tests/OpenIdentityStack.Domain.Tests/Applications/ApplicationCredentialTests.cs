using OpenIdentityStack.Domain.Applications;
using SharedKernel;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Domain.Tests.Applications;

public sealed class ApplicationCredentialTests
{
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly DateTimeOffset now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    public ApplicationCredentialTests()
    {
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(this.now);
    }

    [Fact]
    public void AddSecret_ToConfidentialApplication_AddsActiveClientSecretCredential()
    {
        Application application = this.CreateConfidentialApplication();

        Result<ApplicationCredential> result = application.AddSecret(
            "hashed-secret",
            "Primary secret",
            this.now.AddDays(30),
            this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Type.ShouldBe(ApplicationCredentialType.ClientSecret);
        result.Value.SecretHash.ShouldBe("hashed-secret");
        result.Value.Description.ShouldBe("Primary secret");
        result.Value.CreatedAt.ShouldBe(this.now);
        result.Value.IsActiveAt(this.now).ShouldBeTrue();
        application.Credentials.ShouldContain(result.Value);
    }

    [Fact]
    public void AddCertificate_ToConfidentialApplication_AddsActiveCertificateCredential()
    {
        Application application = this.CreateConfidentialApplication();

        Result<ApplicationCredential> result = application.AddCertificate(
            "ABC123",
            "CN=api.example.com",
            "API certificate",
            this.now.AddYears(1),
            this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Type.ShouldBe(ApplicationCredentialType.X509Certificate);
        result.Value.Thumbprint.ShouldBe("ABC123");
        result.Value.Subject.ShouldBe("CN=api.example.com");
        result.Value.IsActiveAt(this.now).ShouldBeTrue();
    }

    [Fact]
    public void RevokeCredential_WhenCredentialExists_MarksCredentialRevoked()
    {
        Application application = this.CreateConfidentialApplication();
        ApplicationCredential credential = application.AddSecret(
            "hashed-secret",
            null,
            null,
            this.dateTimeProvider).Value;

        Result result = application.RevokeCredential(credential.Id, this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        credential.RevokedAt.ShouldBe(this.now);
        credential.IsActiveAt(this.now).ShouldBeFalse();
    }

    [Fact]
    public void IsActiveAt_WhenCredentialExpired_ReturnsFalse()
    {
        ApplicationCredential credential = ApplicationCredential.CreateClientSecret(
            DomainApplicationId.NewId(),
            "hashed-secret",
            null,
            this.now.AddMinutes(-1),
            this.dateTimeProvider).Value;

        credential.IsActiveAt(this.now).ShouldBeFalse();
    }

    [Fact]
    public void AddSecret_ToPublicApplication_ReturnsCredentialsNotAllowed()
    {
        Application application = this.CreatePublicApplication();

        Result<ApplicationCredential> result = application.AddSecret(
            "hashed-secret",
            null,
            null,
            this.dateTimeProvider);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.CredentialsNotAllowedForPublic.Code);
    }

    private Application CreateConfidentialApplication() =>
        Application.Create(
            "orders-api",
            "Orders API",
            null,
            ApplicationProfile.MachineToMachine,
            OAuthClientType.Confidential,
            ["client_credentials"],
            ["orders.read"],
            [],
            [],
            requirePkce: false,
            requireConsent: false,
            this.dateTimeProvider).Value;

    private Application CreatePublicApplication() =>
        Application.Create(
            "orders-spa",
            "Orders SPA",
            null,
            ApplicationProfile.SinglePage,
            OAuthClientType.Public,
            ["authorization_code"],
            ["openid"],
            ["https://spa.example.com/callback"],
            [],
            requirePkce: true,
            requireConsent: true,
            this.dateTimeProvider).Value;
}
