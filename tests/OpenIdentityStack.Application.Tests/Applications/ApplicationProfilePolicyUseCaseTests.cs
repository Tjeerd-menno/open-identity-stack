using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Domain.Applications;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Application.Tests.Applications;

public sealed class ApplicationProfilePolicyUseCaseTests
{
    private readonly IApplicationRepository repository;
    private readonly IApplicationProtocolProjection projection;
    private readonly IPasswordHasher passwordHasher;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;
    private readonly ApplicationLifecycleUseCases lifecycleUseCases;
    private readonly ApplicationCredentialUseCases credentialUseCases;
    private readonly DateTimeOffset now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    public ApplicationProfilePolicyUseCaseTests()
    {
        this.repository = Substitute.For<IApplicationRepository>();
        this.projection = Substitute.For<IApplicationProtocolProjection>();
        this.passwordHasher = Substitute.For<IPasswordHasher>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.auditLog = Substitute.For<IAuditLog>();
        this.dateTimeProvider.UtcNow.Returns(this.now);
        this.passwordHasher.HashPassword(Arg.Any<string>()).Returns("hashed-secret");
        this.projection.UpsertAsync(Arg.Any<DomainApplication>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        this.projection.UpsertAsync(Arg.Any<DomainApplication>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        this.lifecycleUseCases = new ApplicationLifecycleUseCases(
            this.repository,
            this.projection,
            this.dateTimeProvider,
            this.auditLog);
        this.credentialUseCases = new ApplicationCredentialUseCases(
            this.repository,
            this.projection,
            this.passwordHasher,
            this.dateTimeProvider,
            this.auditLog);
    }

    [Fact]
    public async Task CreateExecuteAsync_WhenTypeIsReserved_ReturnsProfileNotAvailable()
    {
        Result<ApplicationCommandResult> result = await this.lifecycleUseCases.ExecuteAsync(new CreateApplicationCommand(
            "living-room-device",
            "Living Room Device",
            null,
            ApplicationProfile.Device,
            OAuthClientType.Public,
            ["urn:ietf:params:oauth:grant-type:device_code"],
            ["openid"],
            [],
            [],
            RequirePkce: false,
            RequireConsent: true));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.ProfileNotAvailable.Code);
    }

    [Fact]
    public async Task CreateExecuteAsync_WhenSinglePagePkceIsDisabled_ReturnsPkceRequired()
    {
        Result<ApplicationCommandResult> result = await this.lifecycleUseCases.ExecuteAsync(new CreateApplicationCommand(
            "orders-spa",
            "Orders SPA",
            null,
            ApplicationProfile.SinglePage,
            OAuthClientType.Public,
            ["authorization_code"],
            ["openid"],
            ["https://spa.example.com/callback"],
            [],
            RequirePkce: false,
            RequireConsent: true));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.PkceRequired.Code);
    }

    [Fact]
    public async Task CreateExecuteAsync_WhenMachineToMachineEnablesConsent_ReturnsConsentNotAllowed()
    {
        Result<ApplicationCommandResult> result = await this.lifecycleUseCases.ExecuteAsync(new CreateApplicationCommand(
            "orders-worker",
            "Orders Worker",
            null,
            ApplicationProfile.MachineToMachine,
            OAuthClientType.Confidential,
            ["client_credentials"],
            ["api.read"],
            [],
            [],
            RequirePkce: false,
            RequireConsent: true));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.ConsentNotAllowed.Code);
    }

    [Fact]
    public async Task ConfigureExecuteAsync_WhenTypeChanges_ReturnsProfileChangeNotAllowed()
    {
        DomainApplication application = this.CreateWebApplication();
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        Result<ApplicationCommandResult> result = await this.lifecycleUseCases.ExecuteAsync(new ConfigureApplicationOAuthCommand(
            application.Id,
            ApplicationProfile.Native,
            OAuthClientType.Public,
            ["authorization_code"],
            ["openid"],
            ["https://native.example.com/callback"],
            [],
            RequirePkce: true,
            RequireConsent: true));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.ProfileChangeNotAllowed.Code);
    }

    [Fact]
    public async Task ConfigureExecuteAsync_WhenMachineToMachineHasPostLogoutRedirectUri_ReturnsPostLogoutRedirectUrisNotAllowed()
    {
        DomainApplication application = this.CreateMachineToMachineApplication();
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        Result<ApplicationCommandResult> result = await this.lifecycleUseCases.ExecuteAsync(new ConfigureApplicationOAuthCommand(
            application.Id,
            ApplicationProfile.MachineToMachine,
            OAuthClientType.Confidential,
            ["client_credentials"],
            ["api.read"],
            [],
            ["https://worker.example.com/signout"],
            RequirePkce: false,
            RequireConsent: false));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.PostLogoutRedirectUrisNotAllowed.Code);
    }

    [Fact]
    public async Task AddSecretExecuteAsync_WhenApplicationIsPublic_ReturnsCredentialsNotAllowedForPublic()
    {
        DomainApplication application = DomainApplication.Create(
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
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        Result<ApplicationCredentialCommandResult> result = await this.credentialUseCases.ExecuteAsync(
            new AddApplicationSecretCommand(application.Id, null, null, RevokeExisting: false));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.CredentialsNotAllowedForPublic.Code);
    }

    private DomainApplication CreateWebApplication() =>
        DomainApplication.Create(
            "orders-web",
            "Orders Web",
            null,
            ApplicationProfile.Web,
            OAuthClientType.Confidential,
            ["authorization_code"],
            ["openid"],
            ["https://orders.example.com/callback"],
            [],
            requirePkce: true,
            requireConsent: true,
            this.dateTimeProvider).Value;

    private DomainApplication CreateMachineToMachineApplication() =>
        DomainApplication.CreateMachineToMachine(
            "orders-worker",
            "Orders Worker",
            null,
            ["api.read"],
            this.dateTimeProvider).Value;
}
