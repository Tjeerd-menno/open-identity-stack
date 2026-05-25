using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.ServiceAccounts;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Applications;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence.Applications;

public sealed class ServiceAccountToApplicationMigrationTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture fixture;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly DateTimeOffset now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
    private OpenIdentityStackDbContext dbContext = null!;
    private ApplicationMigrationBackfill backfill = null!;

    public ServiceAccountToApplicationMigrationTests(SqliteTestFixture fixture)
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
    public async Task BackfillServiceAccountsAsync_MapsServiceAccountCredentialsAndCertificates()
    {
        ServiceAccount serviceAccount = await this.SeedServiceAccountAsync("worker", ["client_credentials"]);
        Guid secretId = serviceAccount.AddCredential("hashed-secret", "rotation 1", this.now.AddDays(30), this.dateTimeProvider).Value;
        serviceAccount.RecordCredentialUsage(secretId, this.dateTimeProvider);
        Guid certificateId = serviceAccount.AddCertificate("THUMBPRINT", "CN=worker", this.now.AddYears(1), this.dateTimeProvider).Value;
        await this.dbContext.SaveChangesAsync();

        ApplicationMigrationBackfillResult result = await this.backfill.BackfillServiceAccountsAsync();

        result.CreatedApplications.ShouldBe(1);
        DomainApplication application = await this.dbContext.Applications.SingleAsync();
        application.ClientId.ShouldBe("worker");
        application.DisplayName.ShouldBe("Display worker");
        application.Profile.ShouldBe(ApplicationProfile.MachineToMachine);
        application.ClientType.ShouldBe(OAuthClientType.Confidential);
        application.Status.ShouldBe(ApplicationStatus.Active);
        application.AllowedGrantTypes.ShouldBe(["client_credentials"]);
        application.AllowedScopes.ShouldBe(["api"]);
        application.RequiresMigrationReview.ShouldBeFalse();
        application.MigrationSource.ShouldBe("ServiceAccount");

        List<ApplicationCredential> credentials = await this.dbContext.ApplicationCredentials
            .OrderBy(credential => credential.Type)
            .ToListAsync();
        ApplicationCredential secret = credentials.Single(credential => credential.Type == ApplicationCredentialType.ClientSecret);
        secret.Id.ShouldBe(secretId);
        secret.ApplicationId.ShouldBe(application.Id);
        secret.SecretHash.ShouldBe("hashed-secret");
        secret.Description.ShouldBe("rotation 1");
        secret.ExpiresAt.ShouldBe(this.now.AddDays(30));
        secret.LastUsedAt.ShouldBe(this.now);
        secret.RevokedAt.ShouldBeNull();

        ApplicationCredential certificate = credentials.Single(credential => credential.Type == ApplicationCredentialType.X509Certificate);
        certificate.Id.ShouldBe(certificateId);
        certificate.ApplicationId.ShouldBe(application.Id);
        certificate.Thumbprint.ShouldBe("THUMBPRINT");
        certificate.Subject.ShouldBe("CN=worker");
        certificate.Description.ShouldBe("CN=worker");
        certificate.ExpiresAt.ShouldBe(this.now.AddYears(1));
        certificate.RevokedAt.ShouldBeNull();
    }

    [Fact]
    public async Task BackfillServiceAccountsAsync_MapsRevokedLegacyCredentialsWithMigrationTimestamp()
    {
        ServiceAccount serviceAccount = await this.SeedServiceAccountAsync("revoked-worker", ["client_credentials"]);
        Guid secretId = serviceAccount.AddCredential("hashed-secret", "revoked", null, this.dateTimeProvider).Value;
        serviceAccount.RevokeCredential(secretId, this.dateTimeProvider);
        Guid certificateId = serviceAccount.AddCertificate("REVOKEDTHUMB", "CN=revoked", this.now.AddYears(1), this.dateTimeProvider).Value;
        serviceAccount.RevokeCertificate(certificateId, this.dateTimeProvider);
        await this.dbContext.SaveChangesAsync();

        await this.backfill.BackfillServiceAccountsAsync();

        List<ApplicationCredential> credentials = await this.dbContext.ApplicationCredentials.ToListAsync();
        credentials.ShouldAllBe(credential => credential.RevokedAt == this.now);
    }

    [Fact]
    public async Task BackfillServiceAccountsAsync_WhenServiceAccountHasInvalidGrant_FailsBeforeMutation()
    {
        await this.SeedServiceAccountAsync("interactive-worker", ["client_credentials", "authorization_code"]);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => this.backfill.BackfillServiceAccountsAsync());

        exception.Message.ShouldContain("interactive-worker");
        (await this.dbContext.Applications.CountAsync()).ShouldBe(0);
        (await this.dbContext.ApplicationCredentials.CountAsync()).ShouldBe(0);
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
