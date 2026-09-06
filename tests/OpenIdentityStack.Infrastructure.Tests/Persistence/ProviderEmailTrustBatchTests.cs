using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Infrastructure.Identity;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Federation;
using OpenIdentityStack.Infrastructure.Persistence.Migrations;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class ProviderEmailTrustBatchTests(FederationPolicyTestFixture fixture) : IClassFixture<FederationPolicyTestFixture>
{
    [Fact]
    public async Task MigrationCreatesAnIndexThatCanSeekTheNextEvidencePage()
    {
        UpstreamProviderId providerId = await this.SeedAsync();
        await using OpenIdentityStackDbContext db = fixture.CreateDbContext();
        var migration = new IndexActiveProviderEmailEvidence();
        IMigrationsSqlGenerator generator = db.GetService<IMigrationsSqlGenerator>();
        foreach (MigrationCommand command in generator.Generate(migration.DownOperations))
        {
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        }
        foreach (MigrationCommand command in generator.Generate(migration.UpOperations))
        {
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        }
        UserId after = await db.Users.Where(user => user.EmailVerificationEvidence.Any(e => e.ProviderId == providerId.Value))
            .OrderBy(user => user.Id).Skip(100).Select(user => user.Id).FirstAsync();
        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync();
        if (fixture.IsPostgres)
        {
            // Isolate index capability from the cost model for this deliberately small fixture.
            await db.Database.ExecuteSqlRawAsync("SET LOCAL enable_seqscan = off");
        }
        await using DbCommand explain = db.Database.GetDbConnection().CreateCommand();
        explain.Transaction = transaction.GetDbTransaction();
        explain.CommandText = (fixture.IsPostgres ? "EXPLAIN (ANALYZE, FORMAT TEXT) " : "EXPLAIN QUERY PLAN ") +
            """SELECT DISTINCT "UserId" FROM "UserEmailVerificationEvidence" WHERE "ProviderId" = @provider AND "WithdrawnAt" IS NULL AND "UserId" > @after ORDER BY "UserId" LIMIT 100""";
        DbParameter providerParameter = explain.CreateParameter();
        providerParameter.ParameterName = "provider";
        providerParameter.Value = providerId.Value;
        explain.Parameters.Add(providerParameter);
        DbParameter afterParameter = explain.CreateParameter();
        afterParameter.ParameterName = "after";
        afterParameter.Value = after.Value;
        explain.Parameters.Add(afterParameter);
        var plan = new List<string>();
        await using DbDataReader reader = await explain.ExecuteReaderAsync();
        while (await reader.ReadAsync()) { plan.Add(reader.GetString(fixture.IsPostgres ? 0 : 3)); }
        string executionPlan = string.Join(Environment.NewLine, plan);
        executionPlan.ShouldContain("IX_EmailEvidence_ActiveProviderUser");
        if (fixture.IsPostgres)
        {
            executionPlan.ShouldContain("Index Only Scan");
            executionPlan.ShouldContain("Index Cond:");
            executionPlan.ShouldContain("\"UserId\" >");
        }
        else { executionPlan.ShouldContain("UserId>?"); }
    }

    [Fact]
    public async Task WithdrawalSeeksThroughActiveProviderEvidenceInBoundedPages()
    {
        UpstreamProviderId providerId = await this.SeedAsync();
        var observer = new EvidencePageObserver();
        await using OpenIdentityStackDbContext writer = fixture.CreateDbContext(observer);
        (await new ProviderEmailTrustStore(writer, Audit(writer), Invalidator(writer)).SetAsync(providerId, false, "operator", default)).IsSuccess.ShouldBeTrue();

        observer.Commands.Count.ShouldBe(4); // Three pages for 205 users, then the terminal empty page.
        observer.Commands[0].ShouldNotContain(" > ");
        observer.Commands.Skip(1).ShouldAllBe(command => command.Contains(" > ", StringComparison.Ordinal));
        observer.Commands.ShouldAllBe(command => command.Contains("LIMIT", StringComparison.Ordinal)
            && command.Contains("UserEmailVerificationEvidence", StringComparison.Ordinal)
            && !command.Contains("\"Users\"", StringComparison.Ordinal));
        observer.Cursors.Count.ShouldBe(3);
        observer.Cursors.Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public async Task NoOpTrustWithdrawalPreservesVersionEvidenceAndAuditHistory()
    {
        UpstreamProvider provider = UpstreamProvider.Create(
            $"provider-{Guid.NewGuid():N}", "Provider", "https://issuer.example", "client").Value;
        provider.SetEmailVerificationTrust(true);
        User user = User.CreateFederated($"{Guid.NewGuid():N}@example.com", "User", new Clock()).Value;
        user.RecordProviderEmailVerification(provider, "https://issuer.example", user.Email, true, DateTimeOffset.UtcNow);
        provider.SetEmailVerificationTrust(false);
        Guid unchangedVersion = provider.EmailTrustVersion;
        await using (OpenIdentityStackDbContext seed = fixture.CreateDbContext())
        {
            seed.AddRange(provider, user);
            await seed.SaveChangesAsync();
        }

        await using (OpenIdentityStackDbContext writer = fixture.CreateDbContext())
        {
            (await new ProviderEmailTrustStore(writer, Audit(writer), Invalidator(writer))
                .SetAsync(provider.Id, false, "operator", default)).IsSuccess.ShouldBeTrue();
        }

        await using OpenIdentityStackDbContext read = fixture.CreateDbContext();
        UpstreamProvider persistedProvider = await read.UpstreamProviders.SingleAsync(p => p.Id == provider.Id);
        persistedProvider.EmailTrustVersion.ShouldBe(unchangedVersion);
        (await read.Users.SingleAsync(u => u.Id == user.Id)).EmailVerificationEvidence.Single().WithdrawnAt.ShouldBeNull();
        (await read.AuditLogEntries.CountAsync(entry => entry.EntityId == provider.Id.Value.ToString()
            && entry.Action == "Provider.EmailVerificationTrustChanged")).ShouldBe(0);
    }

    [Fact]
    public void ActiveProviderEvidenceHasASeekablePartialIndex()
    {
        using OpenIdentityStackDbContext db = fixture.CreateDbContext();
        Microsoft.EntityFrameworkCore.Metadata.IEntityType evidence = db.Model.FindEntityType(typeof(EmailVerificationEvidence))!;
        evidence.GetIndexes().ShouldContain(index => index.GetDatabaseName() == "IX_EmailEvidence_ActiveProviderUser"
            && index.Properties.Count == 2 && index.Properties[0].Name == "ProviderId" && index.Properties[1].Name == "UserId"
            && index.GetFilter() == "\"WithdrawnAt\" IS NULL");
    }

    [Fact]
    public async Task WithdrawalUsesBoundedTrackingAcrossMultipleBatches()
    {
        UpstreamProviderId providerId = await this.SeedAsync();
        var tracker = new BatchObserver();
        await using OpenIdentityStackDbContext writer = fixture.CreateDbContext(tracker);
        var store = new ProviderEmailTrustStore(writer, Audit(writer), Invalidator(writer));

        (await store.SetAsync(providerId, false, "operator", default)).IsSuccess.ShouldBeTrue();

        tracker.MaximumTrackedUsers.ShouldBeLessThanOrEqualTo(100);
        tracker.Batches.ShouldBeGreaterThan(1);
        tracker.MaximumTrackedEntries.ShouldBeLessThanOrEqualTo(1000);
        await using OpenIdentityStackDbContext read = fixture.CreateDbContext();
        (await read.UpstreamProviders.SingleAsync(p => p.Id == providerId)).TrustEmailVerification.ShouldBeFalse();
        (await read.Users.CountAsync(user => user.EmailVerificationEvidence.Any(e => e.ProviderId == providerId.Value && e.WithdrawnAt == null))).ShouldBe(0);
        (await read.Users.CountAsync(user => user.EmailVerificationEvidence.Any(e => e.ProviderId == null && e.WithdrawnAt == null))).ShouldBeGreaterThan(0);
        (await read.AuditLogEntries.CountAsync(entry => entry.EntityId == providerId.Value.ToString()
            && entry.Action == "Provider.EmailVerificationTrustChanged")).ShouldBe(1);
        (await read.UserSessions.CountAsync(session => session.Status == SessionStatus.Revoked && read.Users.Any(user => user.Id == session.UserId
            && user.EmailVerificationEvidence.Any(e => e.ProviderId == providerId.Value)))).ShouldBe(102);
        (await read.UserSessions.CountAsync(session => session.Status == SessionStatus.Active && read.Users.Any(user => user.Id == session.UserId
            && user.EmailVerificationEvidence.Any(e => e.ProviderId == providerId.Value)))).ShouldBe(103);
    }

    [Fact]
    public async Task LaterBatchFailureRollsBackEarlierEvidenceAndTrust()
    {
        UpstreamProviderId providerId = await this.SeedAsync();
        var tracker = new BatchObserver { FailOnSecondBatch = true };
        await using OpenIdentityStackDbContext writer = fixture.CreateDbContext(tracker);
        var store = new ProviderEmailTrustStore(writer, Audit(writer), Invalidator(writer));

        await Should.ThrowAsync<InvalidOperationException>(() => store.SetAsync(providerId, false, "operator", default));

        tracker.Batches.ShouldBe(2);
        tracker.SuccessfulUserSaves.ShouldBeGreaterThan(0);
        tracker.SavedCredentialAudits.ShouldBeGreaterThan(0);
        await using OpenIdentityStackDbContext read = fixture.CreateDbContext();
        (await read.UpstreamProviders.SingleAsync(p => p.Id == providerId)).TrustEmailVerification.ShouldBeTrue();
        (await read.Users.CountAsync(user => user.EmailVerificationEvidence.Any(e => e.ProviderId == providerId.Value && e.WithdrawnAt == null))).ShouldBe(205);
        (await read.AuditLogEntries.CountAsync(entry => entry.EntityId == providerId.Value.ToString()
            || entry.Action == "Provider.EmailTrustCredentialsRevoked" && entry.Details!.Contains(providerId.Value.ToString()))).ShouldBe(0);
        (await read.UserSessions.CountAsync(session => session.Status == SessionStatus.Active && read.Users.Any(user => user.Id == session.UserId
            && user.EmailVerificationEvidence.Any(e => e.ProviderId == providerId.Value)))).ShouldBe(205);
        (await read.Users.CountAsync(user => user.CredentialRevision != Guid.Empty && user.EmailVerificationEvidence.Any(e => e.ProviderId == providerId.Value))).ShouldBe(0);
    }

    private async Task<UpstreamProviderId> SeedAsync()
    {
        UpstreamProvider provider = UpstreamProvider.Create($"provider-{Guid.NewGuid():N}", "Provider", "https://issuer.example", "client").Value;
        provider.SetEmailVerificationTrust(true);
        await using OpenIdentityStackDbContext seed = fixture.CreateDbContext();
        seed.Add(provider);
        for (int index = 0; index < 205; index++)
        {
            User user = index % 2 == 0
                ? User.CreateLocal($"{Guid.NewGuid():N}@example.com", "User", "hash", new Clock()).Value
                : User.CreateFederated($"{Guid.NewGuid():N}@example.com", "User", new Clock()).Value;
            if (index % 2 == 0) { user.VerifyEmail(new Clock()).IsSuccess.ShouldBeTrue(); }
            user.RecordProviderEmailVerification(provider, "https://issuer.example", user.Email, true, DateTimeOffset.UtcNow);
            seed.Add(user);
            seed.Add(UserSession.Create(user.Id, "127.0.0.1", "Test", new Clock()).Value);
        }
        await seed.SaveChangesAsync();
        return provider.Id;
    }

    private static EmailTrustCredentialInvalidator Invalidator(OpenIdentityStackDbContext db) =>
        new(db, Substitute.For<IOpenIddictTokenManager>(), Substitute.For<IOpenIddictAuthorizationManager>(), new Clock());

    private static AuditLogService Audit(OpenIdentityStackDbContext db) => new(NullLogger<AuditLogService>.Instance, db, new Clock());
    private sealed class Clock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateTimeOffset Now => DateTimeOffset.Now;
    }

    private sealed class EvidencePageObserver : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public List<Guid> Cursors { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("ProviderEmailTrust:active-evidence-batch", StringComparison.Ordinal))
            {
                this.Commands.Add(command.CommandText);
                Guid[] identifiers = command.Parameters.Cast<DbParameter>().Where(parameter => parameter.Value is Guid)
                    .Select(parameter => (Guid)parameter.Value!).ToArray();
                if (identifiers.Length == 2) { this.Cursors.Add(identifiers[1]); }
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class BatchObserver : SaveChangesInterceptor
    {
        private Guid? firstUser;
        public bool FailOnSecondBatch { get; init; }
        public int Batches { get; private set; }
        public int MaximumTrackedUsers { get; private set; }
        public int SuccessfulUserSaves { get; private set; }
        public int MaximumTrackedEntries { get; private set; }
        public int SavedCredentialAudits { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            User[] users = eventData.Context!.ChangeTracker.Entries<User>().Select(entry => entry.Entity).ToArray();
            this.MaximumTrackedUsers = Math.Max(this.MaximumTrackedUsers, users.Length);
            this.MaximumTrackedEntries = Math.Max(this.MaximumTrackedEntries, eventData.Context.ChangeTracker.Entries().Count());
            if (users.Length > 0 && this.firstUser != users[0].Id.Value)
            {
                this.firstUser = users[0].Id.Value;
                this.Batches++;
                if (this.FailOnSecondBatch && this.Batches == 2)
                {
                    throw new InvalidOperationException("Injected second-batch save failure.");
                }
            }
            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<User>().Any()) { this.SuccessfulUserSaves++; }
            if (eventData.Context.ChangeTracker.Entries<AuditLogEntry>().Any(entry => entry.Entity.Action == "Provider.EmailTrustCredentialsRevoked"))
            {
                this.SavedCredentialAudits++;
            }
            return ValueTask.FromResult(result);
        }
    }
}
