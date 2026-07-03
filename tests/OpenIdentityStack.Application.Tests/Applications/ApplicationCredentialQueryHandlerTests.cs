using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Applications.Queries;
using OpenIdentityStack.Domain.Applications;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Application.Tests.Applications;

public sealed class ApplicationCredentialQueryHandlerTests
{
    private readonly IApplicationRepository repository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly ListApplicationCredentialsQueryHandler handler;
    private readonly DateTimeOffset now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    public ApplicationCredentialQueryHandlerTests()
    {
        this.repository = Substitute.For<IApplicationRepository>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(this.now);
        this.handler = new ListApplicationCredentialsQueryHandler(this.repository);
    }

    [Fact]
    public async Task HandleAsync_WhenApplicationExists_ReturnsCredentialMetadataWithoutSecretHashes()
    {
        DomainApplication application = DomainApplication.CreateMachineToMachine(
            "orders-api",
            "Orders API",
            null,
            ["orders.read"],
            this.dateTimeProvider).Value;
        ApplicationCredential secret = application.AddSecret("hashed-secret", "Primary", null, this.dateTimeProvider).Value;
        ApplicationCredential certificate = application.AddCertificate("ABC123", "CN=orders.example.com", null, null, this.dateTimeProvider).Value;
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        Result<IReadOnlyList<ApplicationCredentialDetails>> result = await this.handler.HandleAsync(
            new ListApplicationCredentialsQuery(application.Id));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value.ShouldContain(item =>
            item.Id == secret.Id &&
            item.Type == ApplicationCredentialType.ClientSecret &&
            item.Thumbprint == null &&
            item.Description == "Primary");
        result.Value.ShouldContain(item =>
            item.Id == certificate.Id &&
            item.Type == ApplicationCredentialType.X509Certificate &&
            item.Thumbprint == "ABC123" &&
            item.Subject == "CN=orders.example.com");
    }

    [Fact]
    public async Task HandleAsync_AfterSecretRotation_ReturnsMetadataOnly()
    {
        DomainApplication application = DomainApplication.CreateMachineToMachine(
            "orders-api",
            "Orders API",
            null,
            ["orders.read"],
            this.dateTimeProvider).Value;
        ApplicationCredential firstSecret = application.AddSecret("hashed-secret", "Initial secret", this.now.AddDays(30), this.dateTimeProvider).Value;
        ApplicationCredential rotatedSecret = application.AddSecret("hashed-secret-2", "Rotated secret", this.now.AddDays(60), this.dateTimeProvider).Value;
        firstSecret.Revoke(this.dateTimeProvider);
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        Result<IReadOnlyList<ApplicationCredentialDetails>> result = await this.handler.HandleAsync(
            new ListApplicationCredentialsQuery(application.Id));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        foreach (ApplicationCredentialDetails credential in result.Value)
        {
            credential.GetType().GetProperties()
                .Select(property => property.Name)
                .ShouldNotContain(name => ContainsSecretMaterialName(name));
            credential.Description.ShouldNotBe("hashed-secret");
            credential.Description.ShouldNotBe("hashed-secret-2");
        }

        result.Value.ShouldContain(item => item.Id == firstSecret.Id && item.RevokedAt == this.now);
        result.Value.ShouldContain(item => item.Id == rotatedSecret.Id && item.RevokedAt == null);
    }

    private static bool ContainsSecretMaterialName(string propertyName) =>
        propertyName.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Hash", StringComparison.OrdinalIgnoreCase);
}
