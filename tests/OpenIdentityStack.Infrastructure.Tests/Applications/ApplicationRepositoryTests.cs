using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Infrastructure.Applications;
using OpenIdentityStack.Infrastructure.Identity;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

#pragma warning disable IDE0007 // Use var instead of explicit type
#pragma warning disable IDE0008 // Use explicit type instead of var

namespace OpenIdentityStack.Infrastructure.Tests.Applications;

public sealed class ApplicationRepositoryTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture fixture;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly DateTimeOffset now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
    private OpenIdentityStackDbContext dbContext = null!;
    private ApplicationRepository repository = null!;

    public ApplicationRepositoryTests(SqliteTestFixture fixture)
    {
        this.fixture = fixture;
        IDateTimeProvider provider = Substitute.For<IDateTimeProvider>();
        provider.UtcNow.Returns(this.now);
        this.dateTimeProvider = provider;
    }

    public async ValueTask InitializeAsync()
    {
        await this.fixture.ClearAllDataAsync();
        this.dbContext = this.fixture.CreateDbContext();
        this.repository = new ApplicationRepository(this.dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await this.dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsApplication_WhenExists()
    {
        DomainApplication application = await this.SeedApplicationAsync("orders-web", "Orders Web");

        DomainApplication? result = await this.repository.GetByIdAsync(application.Id);

        result.ShouldNotBeNull();
        result!.ClientId.ShouldBe("orders-web");
        result.DisplayName.ShouldBe("Orders Web");
    }

    [Fact]
    public async Task GetByClientIdAsync_ReturnsApplication_WhenExists()
    {
        await this.SeedApplicationAsync("orders-web", "Orders Web");

        DomainApplication? result = await this.repository.GetByClientIdAsync("orders-web");

        result.ShouldNotBeNull();
        result!.ClientId.ShouldBe("orders-web");
    }

    [Fact]
    public async Task GetByClientIdAsync_LoadsCredentialCollection()
    {
        DomainApplication application = DomainApplication.CreateMachineToMachine(
            "worker-api",
            "Worker API",
            null,
            ["api.read"],
            this.dateTimeProvider).Value;
        ApplicationCredential credential = application.AddSecret(
            "hashed-secret",
            "Primary secret",
            this.now.AddDays(30),
            this.dateTimeProvider).Value;
        await this.repository.AddAsync(application);
        await this.repository.SaveChangesAsync();
        this.dbContext.ChangeTracker.Clear();

        DomainApplication? result = await this.repository.GetByClientIdAsync("worker-api");

        result.ShouldNotBeNull();
        result!.Credentials.Single().Id.ShouldBe(credential.Id);
    }

    [Fact]
    public async Task ExistsByClientIdAsync_ReturnsTrue_WhenClientIdExists()
    {
        await this.SeedApplicationAsync("orders-web", "Orders Web");

        bool exists = await this.repository.ExistsByClientIdAsync("orders-web");

        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ListAsync_ReturnsPagedFilteredApplications()
    {
        await this.SeedApplicationAsync("orders-web", "Orders Web", ApplicationProfile.Web);
        await this.SeedApplicationAsync("billing-m2m", "Billing M2M", ApplicationProfile.MachineToMachine);
        await this.SeedApplicationAsync("management-web", "Management Web", ApplicationProfile.Web);

        (List<DomainApplication> items, int totalCount) = await this.repository.ListAsync(
            1,
            10,
            ApplicationProfile.Web,
            ApplicationStatus.Active,
            OAuthClientType.Confidential,
            "web");

        totalCount.ShouldBe(2);
        items.Count.ShouldBe(2);
        items.ShouldAllBe(application => application.Profile == ApplicationProfile.Web);
        items.ShouldAllBe(application => application.DisplayName.Contains("Web", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AddAsync_PersistsApplicationConfiguration()
    {
        DomainApplication application = this.CreateApplication("new-web", "New Web", ApplicationProfile.Web);

        await this.repository.AddAsync(application);
        await this.repository.SaveChangesAsync();

        await using OpenIdentityStackDbContext freshContext = this.fixture.CreateDbContext();
        DomainApplication? persisted = await freshContext.Applications.FirstOrDefaultAsync(a => a.Id == application.Id);

        persisted.ShouldNotBeNull();
        persisted!.ClientId.ShouldBe("new-web");
        persisted.DisplayName.ShouldBe("New Web");
        persisted.Profile.ShouldBe(ApplicationProfile.Web);
        persisted.ClientType.ShouldBe(OAuthClientType.Confidential);
        persisted.AllowedGrantTypes.ShouldContain("authorization_code");
        persisted.RedirectUris.ShouldContain("https://new-web.example.com/callback");
    }

    [Fact]
    public async Task AddAsync_PersistsApplicationCredentialMetadata()
    {
        DomainApplication application = DomainApplication.CreateMachineToMachine(
            "worker-api",
            "Worker API",
            null,
            ["api.read"],
            this.dateTimeProvider).Value;
        ApplicationCredential secret = application.AddSecret(
            "hashed-secret",
            "Primary secret",
            this.now.AddDays(30),
            this.dateTimeProvider).Value;
        ApplicationCredential certificate = application.AddCertificate(
            "ABC123",
            "CN=worker.example.com",
            "Worker certificate",
            this.now.AddYears(1),
            this.dateTimeProvider).Value;

        await this.repository.AddAsync(application);
        await this.repository.SaveChangesAsync();

        await using OpenIdentityStackDbContext freshContext = this.fixture.CreateDbContext();
        List<ApplicationCredential> credentials = await freshContext.ApplicationCredentials
            .Where(credential => credential.ApplicationId == application.Id)
            .OrderBy(credential => credential.Type)
            .ToListAsync();

        credentials.Count.ShouldBe(2);
        credentials[0].Id.ShouldBe(secret.Id);
        credentials[0].Type.ShouldBe(ApplicationCredentialType.ClientSecret);
        credentials[0].SecretHash.ShouldBe("hashed-secret");
        credentials[0].Description.ShouldBe("Primary secret");
        credentials[0].ExpiresAt.ShouldBe(this.now.AddDays(30));
        credentials[1].Id.ShouldBe(certificate.Id);
        credentials[1].Type.ShouldBe(ApplicationCredentialType.X509Certificate);
        credentials[1].Thumbprint.ShouldBe("ABC123");
        credentials[1].Subject.ShouldBe("CN=worker.example.com");
    }

    [Fact]
    public void ApplicationCredentialMapping_ConfiguresCredentialColumnsAndIndexes()
    {
        IEntityType entity = this.dbContext.Model.FindEntityType(typeof(ApplicationCredential)).ShouldNotBeNull();

        entity.FindProperty(nameof(ApplicationCredential.SecretHash))!.GetMaxLength().ShouldBe(512);
        entity.FindProperty(nameof(ApplicationCredential.Thumbprint))!.GetMaxLength().ShouldBe(128);
        entity.FindProperty(nameof(ApplicationCredential.Subject))!.GetMaxLength().ShouldBe(512);
        entity.FindProperty(nameof(ApplicationCredential.Description))!.GetMaxLength().ShouldBe(1000);
        entity.GetIndexes().Any(index =>
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(ApplicationCredential.ApplicationId),
                nameof(ApplicationCredential.Type),
                nameof(ApplicationCredential.RevokedAt)
            ])).ShouldBeTrue();
        entity.GetIndexes().Any(index =>
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(ApplicationCredential.ApplicationId),
                nameof(ApplicationCredential.Thumbprint)
            ])).ShouldBeTrue();
    }

    [Fact]
    public async Task AddApplicationSecretUseCase_PersistsCredentialForExistingApplication()
    {
        DomainApplication application = await this.SeedApplicationAsync(
            "worker-api",
            "Worker API",
            ApplicationProfile.MachineToMachine);
        IApplicationProtocolProjection projection = Substitute.For<IApplicationProtocolProjection>();
        projection.UpsertAsync(Arg.Any<DomainApplication>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        IAdministrativeClientGuard administrativeGuard = Substitute.For<IAdministrativeClientGuard>();
        administrativeGuard.RequireAsync(Arg.Any<OpenIdentityStack.Domain.Applications.ApplicationId>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var useCase = new ApplicationCredentialUseCases(
            this.repository,
            projection,
            new PasswordHasher(),
            this.dateTimeProvider,
            Substitute.For<IAuditLog>(), administrativeGuard, new UnauthenticatedAdministrativeActorContext());

        Result<ApplicationCredentialCommandResult> result = await useCase.ExecuteAsync(
            new AddApplicationSecretCommand(application.Id, "Primary secret", this.now.AddDays(30), RevokeExisting: false));

        result.IsSuccess.ShouldBeTrue();
        await using OpenIdentityStackDbContext freshContext = this.fixture.CreateDbContext();
        (await freshContext.ApplicationCredentials.CountAsync(credential => credential.ApplicationId == application.Id))
            .ShouldBe(1);
    }

    [Fact]
    public async Task Remove_RemovesApplication()
    {
        DomainApplication application = await this.SeedApplicationAsync("delete-web", "Delete Web");

        this.repository.Remove(application);
        await this.repository.SaveChangesAsync();

        await using OpenIdentityStackDbContext freshContext = this.fixture.CreateDbContext();
        DomainApplication? deleted = await freshContext.Applications.FirstOrDefaultAsync(a => a.Id == application.Id);

        deleted.ShouldBeNull();
    }

    private async Task<DomainApplication> SeedApplicationAsync(
        string clientId,
        string displayName,
        ApplicationProfile profile = ApplicationProfile.Web)
    {
        DomainApplication application = this.CreateApplication(clientId, displayName, profile);
        await this.dbContext.Applications.AddAsync(application);
        await this.dbContext.SaveChangesAsync();
        return application;
    }

    private DomainApplication CreateApplication(
        string clientId,
        string displayName,
        ApplicationProfile profile) =>
        DomainApplication.Create(
            clientId,
            displayName,
            null,
            profile,
            OAuthClientType.Confidential,
            profile == ApplicationProfile.MachineToMachine ? ["client_credentials"] : ["authorization_code"],
            ["openid", "api.read"],
            profile == ApplicationProfile.MachineToMachine ? [] : [$"https://{clientId}.example.com/callback"],
            [],
            requirePkce: false,
            requireConsent: profile != ApplicationProfile.MachineToMachine,
            this.dateTimeProvider).Value;
}

