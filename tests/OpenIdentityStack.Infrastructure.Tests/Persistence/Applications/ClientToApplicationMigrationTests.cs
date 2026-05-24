using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Applications;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;
using DomainClientType = OpenIdentityStack.Domain.Clients.ClientType;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence.Applications;

public sealed class ClientToApplicationMigrationTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture fixture;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly DateTimeOffset now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
    private OpenIdentityStackDbContext dbContext = null!;
    private ApplicationMigrationBackfill backfill = null!;

    public ClientToApplicationMigrationTests(SqliteTestFixture fixture)
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
        this.backfill = new ApplicationMigrationBackfill(this.dbContext, this.dateTimeProvider);
    }

    public async ValueTask DisposeAsync()
    {
        await this.dbContext.DisposeAsync();
    }

    [Fact]
    public async Task BackfillClientsAsync_MapsConfidentialClientToWebApplicationAndPreservesClientId()
    {
        await this.SeedClientAsync(
            "web-client",
            DomainClientType.Confidential,
            ["authorization_code", "refresh_token"],
            redirectUris: ["https://app.example.com/callback"],
            postLogoutRedirectUris: ["https://app.example.com/signout-callback"],
            requirePkce: false,
            requireConsent: true);

        ApplicationMigrationBackfillResult result = await this.backfill.BackfillClientsAsync();

        result.CreatedApplications.ShouldBe(1);
        DomainApplication application = await this.dbContext.Applications.SingleAsync();
        application.ClientId.ShouldBe("web-client");
        application.DisplayName.ShouldBe("Display web-client");
        application.Description.ShouldBe("Legacy client");
        application.Type.ShouldBe(ApplicationType.Web);
        application.ClientType.ShouldBe(OAuthClientType.Confidential);
        application.AllowedGrantTypes.ShouldBe(["authorization_code", "refresh_token"]);
        application.AllowedScopes.ShouldBe(["openid", "api"]);
        application.RedirectUris.ShouldBe(["https://app.example.com/callback"]);
        application.PostLogoutRedirectUris.ShouldBe(["https://app.example.com/signout-callback"]);
        application.RequirePkce.ShouldBeFalse();
        application.RequireConsent.ShouldBeTrue();
        application.RequiresMigrationReview.ShouldBeFalse();
        application.MigrationSource.ShouldBe("Client");
    }

    [Fact]
    public async Task BackfillClientsAsync_MapsPublicAuthorizationCodeClientToReviewableSinglePageApplication()
    {
        await this.SeedClientAsync(
            "spa-client",
            DomainClientType.Public,
            ["authorization_code"],
            redirectUris: ["https://spa.example.com/callback"],
            requirePkce: true,
            requireConsent: false);

        await this.backfill.BackfillClientsAsync();

        DomainApplication application = await this.dbContext.Applications.SingleAsync();
        application.ClientId.ShouldBe("spa-client");
        application.Type.ShouldBe(ApplicationType.SinglePage);
        application.ClientType.ShouldBe(OAuthClientType.Public);
        application.RequiresMigrationReview.ShouldBeTrue();
    }

    [Fact]
    public async Task BackfillClientsAsync_WhenApplicationAlreadyExists_DoesNotCreateDuplicate()
    {
        await this.SeedClientAsync(
            "existing-client",
            DomainClientType.Confidential,
            ["client_credentials"],
            requirePkce: false,
            requireConsent: false);
        Result<DomainApplication> existing = DomainApplication.CreateMachineToMachine(
            "existing-client",
            "Existing application",
            null,
            ["api"],
            this.dateTimeProvider);
        existing.IsSuccess.ShouldBeTrue();
        await this.dbContext.Applications.AddAsync(existing.Value);
        await this.dbContext.SaveChangesAsync();

        ApplicationMigrationBackfillResult result = await this.backfill.BackfillClientsAsync();

        result.CreatedApplications.ShouldBe(0);
        (await this.dbContext.Applications.CountAsync()).ShouldBe(1);
    }

    private async Task<Client> SeedClientAsync(
        string clientId,
        DomainClientType clientType,
        IReadOnlyList<string> allowedGrantTypes,
        IReadOnlyList<string>? redirectUris = null,
        IReadOnlyList<string>? postLogoutRedirectUris = null,
        bool requirePkce = false,
        bool requireConsent = false)
    {
        Result<Client> result = Client.Create(
            clientId,
            $"Display {clientId}",
            clientType,
            "Legacy client",
            redirectUris ?? [],
            postLogoutRedirectUris ?? [],
            ["openid", "api"],
            allowedGrantTypes,
            requirePkce,
            requireConsent,
            this.dateTimeProvider);
        result.IsSuccess.ShouldBeTrue();

        Client client = result.Value;
        await this.dbContext.Clients.AddAsync(client);
        await this.dbContext.SaveChangesAsync();
        return client;
    }
}
