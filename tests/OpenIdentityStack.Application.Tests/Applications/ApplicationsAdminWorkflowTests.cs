using Microsoft.Extensions.DependencyInjection;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Application.Applications.Queries;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Domain.Applications;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Tests.Applications;

public sealed class ApplicationsAdminWorkflowTests
{
    private static readonly string[] AuthorizationCodeGrantTypes = ["authorization_code"];
    private static readonly string[] OrdersCallbackRedirectUris = ["https://orders.example.com/callback"];

    private readonly IApplicationRepository repository;
    private readonly IApplicationProtocolProjection projection;
    private readonly IPasswordHasher passwordHasher;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;
    private readonly ApplicationsAdminWorkflow workflow;
    private readonly DateTimeOffset now = new(2026, 6, 8, 12, 0, 0, TimeSpan.Zero);

    public ApplicationsAdminWorkflowTests()
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
        this.projection.DeleteAsync(Arg.Any<DomainApplicationId>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IAdministrativeClientGuard administrativeGuard = Substitute.For<IAdministrativeClientGuard>();
        administrativeGuard.RequireAsync(Arg.Any<DomainApplicationId>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        ApplicationLifecycleUseCases lifecycleUseCases = new(
            this.repository,
            this.projection,
            this.passwordHasher,
            this.dateTimeProvider,
            this.auditLog, administrativeGuard, Substitute.For<IApplicationProtocolProjectionTransaction>());
        ApplicationCredentialUseCases credentialUseCases = new(
            this.repository,
            this.projection,
            this.passwordHasher,
            this.dateTimeProvider,
            this.auditLog, administrativeGuard);

        this.workflow = new ApplicationsAdminWorkflow(
            lifecycleUseCases,
            credentialUseCases);
    }

    [Fact]
    public async Task CreateAsync_DelegatesCreateCommandAndReturnsCreatedDetails()
    {
        using var cts = new CancellationTokenSource();
        CreateApplicationAdminWorkflowRequest request = new(
            ClientId: "orders-web",
            DisplayName: "Orders Web",
            Description: "Orders portal",
            Profile: ApplicationProfile.Web,
            ClientType: OAuthClientType.Confidential,
            AllowedGrantTypes: ["authorization_code"],
            AllowedScopes: ["openid", "orders.read"],
            RedirectUris: ["https://orders.example.com/callback"],
            PostLogoutRedirectUris: ["https://orders.example.com/signout-callback"],
            RequirePkce: false,
            RequireConsent: true);

        DomainApplication? addedApplication = null;
        this.repository
            .AddAsync(Arg.Do<DomainApplication>(application => addedApplication = application), cts.Token)
            .Returns(Task.CompletedTask);

        Result<ApplicationCreateOperationResult> result = await this.workflow.CreateAsync(request, cts.Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.InitialSecret.ShouldBeNull();
        result.Value.Details.ClientId.ShouldBe("orders-web");
        result.Value.Details.DisplayName.ShouldBe("Orders Web");
        result.Value.Details.Description.ShouldBe("Orders portal");
        result.Value.Details.Profile.ShouldBe(ApplicationProfile.Web);
        result.Value.Details.ClientType.ShouldBe(OAuthClientType.Confidential);
        result.Value.Details.Status.ShouldBe(ApplicationStatus.Active);
        result.Value.Details.AllowedGrantTypes.ShouldBe(AuthorizationCodeGrantTypes);
        result.Value.Details.RedirectUris.ShouldBe(OrdersCallbackRedirectUris);
        await this.repository.Received(1).AddAsync(
            Arg.Is<DomainApplication>(application =>
                application!.ClientId == "orders-web" &&
                application.Description == "Orders portal" &&
                application.AllowedGrantTypes.SequenceEqual(AuthorizationCodeGrantTypes) &&
                application.RedirectUris.SequenceEqual(OrdersCallbackRedirectUris)),
            cts.Token);
        await this.repository.DidNotReceive().GetByIdAsync(Arg.Any<DomainApplicationId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenNoInitialCredentialRequested_ReturnsDetailsWithoutSecretMaterial()
    {
        CreateApplicationAdminWorkflowRequest request = new(
            ClientId: "orders-web",
            DisplayName: "Orders Web",
            Description: "Orders portal",
            Profile: ApplicationProfile.Web,
            ClientType: OAuthClientType.Confidential,
            AllowedGrantTypes: ["authorization_code"],
            AllowedScopes: ["openid", "orders.read"],
            RedirectUris: ["https://orders.example.com/callback"],
            PostLogoutRedirectUris: ["https://orders.example.com/signout-callback"],
            RequirePkce: false,
            RequireConsent: true);

        Result<ApplicationCreateOperationResult> result = await this.workflow.CreateAsync(request);

        result.IsSuccess.ShouldBeTrue();
        result.Value.InitialSecret.ShouldBeNull();
        result.Value.Details.GetType().GetProperties()
            .Select(property => property.Name)
            .ShouldNotContain(name => ContainsSecretMaterialName(name));
    }

    [Fact]
    public async Task CreateAsync_WithInitialClientSecret_ReturnsInitialSecretOnlyOnCreateResult()
    {
        DomainApplication? addedApplication = null;
        this.repository
            .AddAsync(Arg.Do<DomainApplication>(application => addedApplication = application), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        this.repository.GetByIdAsync(Arg.Any<DomainApplicationId>(), Arg.Any<CancellationToken>())
            .Returns(_ => addedApplication);
        CreateApplicationAdminWorkflowRequest request = new(
            ClientId: "orders-api",
            DisplayName: "Orders API",
            Description: "Orders service",
            Profile: ApplicationProfile.MachineToMachine,
            ClientType: OAuthClientType.Confidential,
            AllowedGrantTypes: ["client_credentials"],
            AllowedScopes: ["orders.read"],
            RedirectUris: [],
            PostLogoutRedirectUris: [],
            RequirePkce: false,
            RequireConsent: false,
            InitialCredential: new CreateInitialCredentialAdminWorkflowRequest(
                Type: "ClientSecret",
                Description: "Initial secret",
                ExpiresAt: this.now.AddDays(30)));

        Result<ApplicationCreateOperationResult> createResult = await this.workflow.CreateAsync(request);
        ListApplicationCredentialsQueryHandler listCredentialsQueryHandler = new(this.repository);
        Result<IReadOnlyList<ApplicationCredentialDetails>> credentialsResult = await listCredentialsQueryHandler.HandleAsync(
            new ListApplicationCredentialsQuery(createResult.Value.Details.Id));

        createResult.IsSuccess.ShouldBeTrue();
        createResult.Value.InitialSecret.ShouldNotBeNullOrWhiteSpace();
        createResult.Value.Details.GetType().GetProperties()
            .Select(property => property.Name)
            .ShouldNotContain(name => ContainsSecretMaterialName(name));
        credentialsResult.IsSuccess.ShouldBeTrue();
        ApplicationCredentialDetails credential = credentialsResult.Value.Single();
        credential.Description.ShouldBe("Initial secret");
        credential.Description.ShouldNotBe(createResult.Value.InitialSecret);
        credential.Description.ShouldNotBe("hashed-secret");
        credential.GetType().GetProperties()
            .Select(property => property.Name)
            .ShouldNotContain(name => ContainsSecretMaterialName(name));
    }

    [Fact]
    public async Task CreateAsync_WhenInitialSecretProjectionFails_DoesNotPersistApplication()
    {
        this.projection.UpsertAsync(Arg.Any<DomainApplication>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DomainError.Failure("Projection.Failed", "Projection failed."));
        CreateApplicationAdminWorkflowRequest request = new(
            ClientId: "orders-api",
            DisplayName: "Orders API",
            Description: "Orders service",
            Profile: ApplicationProfile.MachineToMachine,
            ClientType: OAuthClientType.Confidential,
            AllowedGrantTypes: ["client_credentials"],
            AllowedScopes: ["orders.read"],
            RedirectUris: [],
            PostLogoutRedirectUris: [],
            RequirePkce: false,
            RequireConsent: false,
            InitialCredential: new CreateInitialCredentialAdminWorkflowRequest(
                Type: "ClientSecret",
                Description: "Initial secret",
                ExpiresAt: this.now.AddDays(30)));

        Result<ApplicationCreateOperationResult> result = await this.workflow.CreateAsync(request);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Failure.Projection.Failed");
        await this.repository.DidNotReceive().AddAsync(Arg.Any<DomainApplication>(), Arg.Any<CancellationToken>());
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await this.auditLog.DidNotReceive().LogAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WhenInitialCredentialTypeIsMissing_ReturnsValidationError(string? type)
    {
        CreateApplicationAdminWorkflowRequest request = new(
            ClientId: "orders-api",
            DisplayName: "Orders API",
            Description: "Orders service",
            Profile: ApplicationProfile.MachineToMachine,
            ClientType: OAuthClientType.Confidential,
            AllowedGrantTypes: ["client_credentials"],
            AllowedScopes: ["orders.read"],
            RedirectUris: [],
            PostLogoutRedirectUris: [],
            RequirePkce: false,
            RequireConsent: false,
            InitialCredential: new CreateInitialCredentialAdminWorkflowRequest(
                Type: type!,
                Description: "Initial secret",
                ExpiresAt: this.now.AddDays(30)));

        Result<ApplicationCreateOperationResult> result = await this.workflow.CreateAsync(request);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.ApplicationCredential.InitialCredentialTypeInvalid");
        await this.repository.DidNotReceive().AddAsync(Arg.Any<DomainApplication>(), Arg.Any<CancellationToken>());
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfigureOAuthAsync_DelegatesConfigureCommandAndReturnsUpdatedDetails()
    {
        using var cts = new CancellationTokenSource();
        DomainApplication application = this.CreateApplication();
        this.repository.GetByIdAsync(application.Id, cts.Token)
            .Returns(application);

        ConfigureApplicationOAuthAdminWorkflowRequest request = new(
            application.Id,
            Profile: ApplicationProfile.Web,
            ClientType: OAuthClientType.Confidential,
            AllowedGrantTypes: ["authorization_code"],
            AllowedScopes: ["openid", "orders.write"],
            RedirectUris: ["https://orders.example.com/oauth/callback"],
            PostLogoutRedirectUris: ["https://orders.example.com/oauth/signout"],
            RequirePkce: true,
            RequireConsent: true);

        Result<ApplicationDetails> result = await this.workflow.ConfigureOAuthAsync(request, cts.Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(application.Id);
        result.Value.Profile.ShouldBe(ApplicationProfile.Web);
        result.Value.AllowedScopes.ShouldBe(["openid", "orders.write"]);
        result.Value.RedirectUris.ShouldBe(["https://orders.example.com/oauth/callback"]);
        application.AllowedGrantTypes.ShouldBe(["authorization_code"]);
        application.AllowedScopes.ShouldBe(["openid", "orders.write"]);
        application.RedirectUris.ShouldBe(["https://orders.example.com/oauth/callback"]);
        await this.projection.Received(1).UpsertAsync(application, cts.Token);
        await this.repository.Received(1).GetByIdAsync(application.Id, cts.Token);
    }

    [Fact]
    public async Task EnableAndDisableAsync_DelegateLifecycleCommandsAndReturnUpdatedDetails()
    {
        using var cts = new CancellationTokenSource();
        DomainApplication application = this.CreateApplication();
        this.repository.GetByIdAsync(application.Id, cts.Token)
            .Returns(application);

        Result<ApplicationDetails> disableResult = await this.workflow.DisableAsync(new DisableApplicationAdminWorkflowRequest(application.Id), cts.Token);
        Result<ApplicationDetails> enableResult = await this.workflow.EnableAsync(new EnableApplicationAdminWorkflowRequest(application.Id), cts.Token);

        disableResult.IsSuccess.ShouldBeTrue();
        enableResult.IsSuccess.ShouldBeTrue();
        disableResult.Value.Status.ShouldBe(ApplicationStatus.Disabled);
        enableResult.Value.Status.ShouldBe(ApplicationStatus.Active);
        application.Status.ShouldBe(ApplicationStatus.Active);
        await this.auditLog.Received(1).LogAsync(
            "system",
            "Application.Disabled",
            "Application",
            application.Id.Value.ToString(),
            "ClientId: orders-web",
            cts.Token);
        await this.auditLog.Received(1).LogAsync(
            "system",
            "Application.Enabled",
            "Application",
            application.Id.Value.ToString(),
            "ClientId: orders-web",
            cts.Token);
        await this.repository.Received(2).GetByIdAsync(application.Id, cts.Token);
    }

    [Fact]
    public async Task AddSecretAsync_ShapesCredentialCommandAsOneTimeSecretOperationResult()
    {
        DomainApplication application = this.CreateMachineToMachineApplication();
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);
        DateTimeOffset expiresAt = this.now.AddDays(30);

        Result<ApplicationSecretOperationResult> result = await this.workflow.AddSecretAsync(
            new AddApplicationSecretAdminWorkflowRequest(
                application.Id,
                Description: "Rotated secret",
                ExpiresAt: expiresAt,
                RevokeExisting: true));

        result.IsSuccess.ShouldBeTrue();
        result.Value.CredentialId.ShouldNotBe(Guid.Empty);
        result.Value.OneTimeSecret.ShouldNotBeNullOrWhiteSpace();
        ApplicationCredential credential = application.Credentials.Single();
        result.Value.CredentialId.ShouldBe(credential.Id);
        credential.Description.ShouldBe("Rotated secret");
        credential.ExpiresAt.ShouldBe(expiresAt);
        credential.SecretHash.ShouldBe("hashed-secret");
        await this.projection.Received(1).UpsertAsync(application, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddCertificateAndRevokeCredentialAsync_DelegateCredentialCommands()
    {
        DomainApplication application = this.CreateMachineToMachineApplication();
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        Result<ApplicationCredentialCommandResult> addResult = await this.workflow.AddCertificateAsync(
            new AddApplicationCertificateAdminWorkflowRequest(
                application.Id,
                Thumbprint: "ABC123",
                Subject: "CN=orders.example.com",
                Description: "Orders certificate",
                ExpiresAt: this.now.AddYears(1)));
        Result revokeResult = await this.workflow.RevokeCredentialAsync(
            new RevokeApplicationCredentialAdminWorkflowRequest(application.Id, addResult.Value.CredentialId));

        addResult.IsSuccess.ShouldBeTrue();
        addResult.Value.Type.ShouldBe(ApplicationCredentialType.X509Certificate);
        addResult.Value.OneTimeSecret.ShouldBeNull();
        revokeResult.IsSuccess.ShouldBeTrue();
        application.Credentials.Single().RevokedAt.ShouldBe(this.now);
        await this.projection.Received(1).UpsertAsync(application, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AddApplication_ResolvesApplicationsAdminWorkflow()
    {
        ServiceCollection services = new();
        services.AddScoped(_ => Substitute.For<IApplicationRepository>());
        services.AddScoped(_ => Substitute.For<IApplicationProtocolProjection>());
        services.AddScoped(_ => Substitute.For<IApplicationProtocolProjectionTransaction>());
        services.AddScoped(_ => Substitute.For<IPasswordHasher>());
        services.AddScoped(_ => Substitute.For<IDateTimeProvider>());
        services.AddScoped(_ => Substitute.For<IAuditLog>());

        services.AddApplication();
        services.AddScoped(_ => Substitute.For<IAdministrativeClientGuard>());
        using ServiceProvider provider = services.BuildServiceProvider();

        IApplicationsAdminWorkflow workflow = provider.GetRequiredService<IApplicationsAdminWorkflow>();

        workflow.ShouldBeOfType<ApplicationsAdminWorkflow>();
    }

    private DomainApplication CreateApplication() =>
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
            requirePkce: false,
            requireConsent: true,
            this.dateTimeProvider).Value;

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
