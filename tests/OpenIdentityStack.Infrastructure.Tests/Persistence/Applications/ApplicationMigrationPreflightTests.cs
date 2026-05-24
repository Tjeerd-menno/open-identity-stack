using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Domain.ServiceAccounts;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Applications;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;
using DomainClientType = OpenIdentityStack.Domain.Clients.ClientType;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence.Applications;

public sealed class ApplicationMigrationPreflightTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture fixture;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly DateTimeOffset now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
    private OpenIdentityStackDbContext dbContext = null!;
    private ApplicationMigrationPreflight preflight = null!;

    public ApplicationMigrationPreflightTests(SqliteTestFixture fixture)
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
        this.preflight = new ApplicationMigrationPreflight(this.dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await this.dbContext.DisposeAsync();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenClientAndServiceAccountShareClientId_ReturnsBlockingDuplicateIssue()
    {
        await this.SeedClientAsync("shared-client", DomainClientType.Confidential, ["client_credentials"]);
        await this.SeedServiceAccountAsync("shared-client", ["client_credentials"]);

        ApplicationMigrationPreflightReport report = await this.preflight.AnalyzeAsync();

        report.CanMigrate.ShouldBeFalse();
        ApplicationMigrationPreflightIssue issue = report.BlockingIssues.Single();
        issue.Code.ShouldBe(ApplicationMigrationPreflightIssueCodes.DuplicateClientId);
        issue.ClientId.ShouldBe("shared-client");
        issue.Sources.ShouldBe(["Client", "ServiceAccount"]);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenServiceAccountUsesNonClientCredentialsGrant_ReturnsBlockingIssue()
    {
        await this.SeedServiceAccountAsync("interactive-service-account", ["client_credentials", "authorization_code"]);

        ApplicationMigrationPreflightReport report = await this.preflight.AnalyzeAsync();

        report.CanMigrate.ShouldBeFalse();
        ApplicationMigrationPreflightIssue issue = report.BlockingIssues.Single();
        issue.Code.ShouldBe(ApplicationMigrationPreflightIssueCodes.InvalidServiceAccountGrant);
        issue.ClientId.ShouldBe("interactive-service-account");
        issue.Details.ShouldContain("authorization_code");
    }

    [Fact]
    public async Task AnalyzeAsync_WhenClientProfileNeedsReview_ReturnsReviewItemWithoutBlockingMigration()
    {
        await this.SeedClientAsync(
            "spa-client",
            DomainClientType.Public,
            ["authorization_code"],
            redirectUris: ["https://spa.example.com/callback"]);

        ApplicationMigrationPreflightReport report = await this.preflight.AnalyzeAsync();

        report.CanMigrate.ShouldBeTrue();
        report.BlockingIssues.ShouldBeEmpty();
        ApplicationMigrationReviewItem review = report.ReviewItems.Single();
        review.ClientId.ShouldBe("spa-client");
        review.Source.ShouldBe("Client");
        review.InferredType.ShouldBe(ApplicationType.SinglePage);
        review.RequiresMigrationReview.ShouldBeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenPreflightFails_DoesNotCreateApplicationsOrCredentials()
    {
        await this.SeedClientAsync("shared-client", DomainClientType.Confidential, ["client_credentials"]);
        ServiceAccount serviceAccount = await this.SeedServiceAccountAsync("shared-client", ["authorization_code"]);
        serviceAccount.AddCredential("hashed-secret", "legacy secret", null, this.dateTimeProvider);
        await this.dbContext.SaveChangesAsync();

        ApplicationMigrationPreflightReport report = await this.preflight.AnalyzeAsync();

        report.CanMigrate.ShouldBeFalse();
        (await this.dbContext.Applications.CountAsync()).ShouldBe(0);
        (await this.dbContext.ApplicationCredentials.CountAsync()).ShouldBe(0);
    }

    private async Task<Client> SeedClientAsync(
        string clientId,
        DomainClientType clientType,
        IReadOnlyList<string> allowedGrantTypes,
        IReadOnlyList<string>? redirectUris = null)
    {
        Result<Client> result = Client.Create(
            clientId,
            $"Display {clientId}",
            clientType,
            "Legacy client",
            redirectUris ?? [],
            [],
            ["openid", "api"],
            allowedGrantTypes,
            requirePkce: clientType == DomainClientType.Public,
            requireConsent: clientType == DomainClientType.Public,
            this.dateTimeProvider);
        result.IsSuccess.ShouldBeTrue();

        Client client = result.Value;
        await this.dbContext.Clients.AddAsync(client);
        await this.dbContext.SaveChangesAsync();
        return client;
    }

    private async Task<ServiceAccount> SeedServiceAccountAsync(
        string clientId,
        IReadOnlyList<string> allowedGrantTypes)
    {
        Result<ServiceAccount> result = ServiceAccount.Create(
            clientId,
            $"Display {clientId}",
            ["api"],
            allowedGrantTypes,
            this.dateTimeProvider);
        result.IsSuccess.ShouldBeTrue();

        ServiceAccount serviceAccount = result.Value;
        await this.dbContext.ServiceAccounts.AddAsync(serviceAccount);
        await this.dbContext.SaveChangesAsync();
        return serviceAccount;
    }
}
