using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Application.Applications.Queries;
using OpenIdentityStack.Domain.Applications;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Application.Tests.Applications;

public sealed class ApplicationCredentialUseCaseTests
{
    private readonly IApplicationRepository repository;
    private readonly IApplicationProtocolProjection projection;
    private readonly IPasswordHasher passwordHasher;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;
    private readonly ApplicationCredentialUseCases credentialUseCases;
    private readonly ApplicationCredentialValidationUseCases validationUseCases;
    private readonly DateTimeOffset now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    public ApplicationCredentialUseCaseTests()
    {
        this.repository = Substitute.For<IApplicationRepository>();
        this.projection = Substitute.For<IApplicationProtocolProjection>();
        this.passwordHasher = Substitute.For<IPasswordHasher>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.auditLog = Substitute.For<IAuditLog>();
        IAdministrativeClientGuard administrativeGuard = Substitute.For<IAdministrativeClientGuard>();
        administrativeGuard.RequireAsync(Arg.Any<OpenIdentityStack.Domain.Applications.ApplicationId>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        this.dateTimeProvider.UtcNow.Returns(this.now);
        this.passwordHasher.HashPassword(Arg.Any<string>()).Returns("hashed-secret");
        this.projection.UpsertAsync(Arg.Any<DomainApplication>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        this.projection.UpsertAsync(Arg.Any<DomainApplication>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        this.credentialUseCases = new ApplicationCredentialUseCases(
            this.repository,
            this.projection,
            this.passwordHasher,
            this.dateTimeProvider,
            this.auditLog, administrativeGuard);
        this.validationUseCases = new ApplicationCredentialValidationUseCases(
            this.repository,
            this.passwordHasher,
            this.dateTimeProvider,
            this.auditLog);
    }

    [Fact]
    public async Task AddSecretExecuteAsync_WhenApplicationExists_AddsHashedCredentialAndReturnsOneTimeSecret()
    {
        DomainApplication application = this.CreateMachineToMachineApplication();
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        Result<ApplicationCredentialCommandResult> result = await this.credentialUseCases.ExecuteAsync(
            new AddApplicationSecretCommand(application.Id, "Initial secret", this.now.AddDays(30), RevokeExisting: false));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ApplicationId.ShouldBe(application.Id);
        result.Value.Type.ShouldBe(ApplicationCredentialType.ClientSecret);
        result.Value.OneTimeSecret.ShouldNotBeNullOrWhiteSpace();
        application.Credentials.Single().SecretHash.ShouldBe("hashed-secret");
        await this.projection.Received(1).UpsertAsync(application, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await this.auditLog.Received(1).LogAsync(
            "system",
            "ApplicationCredential.SecretAdded",
            "Application",
            application.Id.Value.ToString(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeCredentialExecuteAsync_WhenCredentialExists_RevokesCredential()
    {
        DomainApplication application = this.CreateMachineToMachineApplication();
        ApplicationCredential credential = application.AddSecret("hashed-secret", null, null, this.dateTimeProvider).Value;
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        Result result = await this.credentialUseCases.ExecuteAsync(
            new RevokeApplicationCredentialCommand(application.Id, credential.Id));

        result.IsSuccess.ShouldBeTrue();
        credential.RevokedAt.ShouldBe(this.now);

        // Revoking a credential must sync protocol state without rotating the client secret.
        await this.projection.Received(1).UpsertAsync(application, Arg.Any<CancellationToken>());
        await this.projection.DidNotReceive().UpsertAsync(
            Arg.Any<DomainApplication>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddCertificateExecuteAsync_WhenApplicationExists_AddsCertificateCredential()
    {
        DomainApplication application = this.CreateMachineToMachineApplication();
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        Result<ApplicationCredentialCommandResult> result = await this.credentialUseCases.ExecuteAsync(
            new AddApplicationCertificateCommand(
                application.Id,
                "ABC123",
                "CN=orders.example.com",
                "Orders certificate",
                this.now.AddYears(1)));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Type.ShouldBe(ApplicationCredentialType.X509Certificate);
        application.Credentials.Single().Thumbprint.ShouldBe("ABC123");
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListCredentialsAfterSecretRotation_ReturnsMetadataWithoutSecretMaterial()
    {
        DomainApplication application = this.CreateMachineToMachineApplication();
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);
        var listCredentials = new ListApplicationCredentialsQueryHandler(this.repository);

        Result<ApplicationCredentialCommandResult> firstSecret = await this.credentialUseCases.ExecuteAsync(
            new AddApplicationSecretCommand(application.Id, "Initial secret", this.now.AddDays(30), RevokeExisting: false));
        Result<ApplicationCredentialCommandResult> rotatedSecret = await this.credentialUseCases.ExecuteAsync(
            new AddApplicationSecretCommand(application.Id, "Rotated secret", this.now.AddDays(60), RevokeExisting: true));

        Result<IReadOnlyList<ApplicationCredentialDetails>> result = await listCredentials.HandleAsync(
            new ListApplicationCredentialsQuery(application.Id));

        firstSecret.Value.OneTimeSecret.ShouldNotBeNullOrWhiteSpace();
        rotatedSecret.Value.OneTimeSecret.ShouldNotBeNullOrWhiteSpace();
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        foreach (ApplicationCredentialDetails credential in result.Value)
        {
            typeof(ApplicationCredentialDetails).GetProperties()
                .Select(property => property.Name)
                .ShouldNotContain(name => ContainsSecretMaterialName(name));
            credential.Description.ShouldNotBe(firstSecret.Value.OneTimeSecret);
            credential.Description.ShouldNotBe(rotatedSecret.Value.OneTimeSecret);
            credential.Description.ShouldNotBe("hashed-secret");
        }
    }

    [Fact]
    public async Task ValidateClientCredentialsExecuteAsync_WithActiveSecret_ReturnsApplicationAndRecordsUsage()
    {
        DomainApplication application = this.CreateMachineToMachineApplication();
        ApplicationCredential credential = application.AddSecret("hashed-secret", null, null, this.dateTimeProvider).Value;
        this.repository.GetByClientIdAsync(application.ClientId, Arg.Any<CancellationToken>())
            .Returns(application);
        this.passwordHasher.VerifyPassword("hashed-secret", "plain-secret").Returns(true);

        Result<ValidateApplicationCredentialsResult> result = await this.validationUseCases.ExecuteAsync(
            new ValidateApplicationClientCredentialsCommand(application.ClientId, "plain-secret"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ApplicationId.ShouldBe(application.Id);
        result.Value.AllowedGrantTypes.ShouldBe(["client_credentials"]);
        credential.LastUsedAt.ShouldBe(this.now);
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await this.auditLog.Received(1).LogAsync(
            "system",
            "ApplicationCredential.Used",
            "Application",
            application.Id.Value.ToString(),
            Arg.Is<string>(details =>
                details!.Contains(credential.Id.ToString(), StringComparison.Ordinal) &&
                details.Contains("ClientSecret", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateCertificateExecuteAsync_WithActiveCertificate_ReturnsApplicationAndRecordsUsage()
    {
        DomainApplication application = this.CreateMachineToMachineApplication();
        ApplicationCredential credential = application.AddCertificate("ABC123", "CN=orders.example.com", null, null, this.dateTimeProvider).Value;
        this.repository.GetByClientIdAsync(application.ClientId, Arg.Any<CancellationToken>())
            .Returns(application);

        Result<ValidateApplicationCredentialsResult> result = await this.validationUseCases.ExecuteAsync(
            new ValidateApplicationCertificateCommand(application.ClientId, "ABC123"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ApplicationId.ShouldBe(application.Id);
        credential.LastUsedAt.ShouldBe(this.now);
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await this.auditLog.Received(1).LogAsync(
            "system",
            "ApplicationCredential.Used",
            "Application",
            application.Id.Value.ToString(),
            Arg.Is<string>(details =>
                details!.Contains(credential.Id.ToString(), StringComparison.Ordinal) &&
                details.Contains("X509Certificate", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    private DomainApplication CreateMachineToMachineApplication() =>
        DomainApplication.CreateMachineToMachine(
            "orders-api",
            "Orders API",
            null,
            ["orders.read"],
            this.dateTimeProvider).Value;

    private static bool ContainsSecretMaterialName(string propertyName) =>
        propertyName.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Hash", StringComparison.OrdinalIgnoreCase);
}
