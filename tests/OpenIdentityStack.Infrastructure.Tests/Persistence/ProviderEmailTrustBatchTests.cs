using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Federation;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class ProviderEmailTrustBatchTests(FederationPolicyTestFixture fixture) : IClassFixture<FederationPolicyTestFixture>
{
    [Fact]
    public async Task WithdrawalUsesBoundedTrackingAcrossMultipleBatches()
    {
        UpstreamProviderId providerId = await this.SeedAsync();
        var tracker = new BatchObserver();
        await using OpenIdentityStackDbContext writer = fixture.CreateDbContext(tracker);
        var store = new ProviderEmailTrustStore(writer, Audit(writer));

        (await store.SetAsync(providerId, false, "operator", default)).IsSuccess.ShouldBeTrue();

        tracker.MaximumTrackedUsers.ShouldBeLessThanOrEqualTo(100);
        tracker.Batches.ShouldBeGreaterThan(1);
        await using OpenIdentityStackDbContext read = fixture.CreateDbContext();
        (await read.UpstreamProviders.SingleAsync(p => p.Id == providerId)).TrustEmailVerification.ShouldBeFalse();
        (await read.Users.CountAsync(user => user.EmailVerificationEvidence.Any(e => e.ProviderId == providerId.Value && e.WithdrawnAt == null))).ShouldBe(0);
        (await read.Users.CountAsync(user => user.EmailVerificationEvidence.Any(e => e.ProviderId == null && e.WithdrawnAt == null))).ShouldBeGreaterThan(0);
        (await read.AuditLogEntries.CountAsync(entry => entry.EntityId == providerId.Value.ToString()
            && entry.Action == "Provider.EmailVerificationTrustChanged")).ShouldBe(1);
    }

    [Fact]
    public async Task LaterBatchFailureRollsBackEarlierEvidenceAndTrust()
    {
        UpstreamProviderId providerId = await this.SeedAsync();
        var tracker = new BatchObserver { FailOnSecondBatch = true };
        await using OpenIdentityStackDbContext writer = fixture.CreateDbContext(tracker);
        var store = new ProviderEmailTrustStore(writer, Audit(writer));

        await Should.ThrowAsync<InvalidOperationException>(() => store.SetAsync(providerId, false, "operator", default));

        tracker.Batches.ShouldBe(2);
        tracker.SuccessfulUserSaves.ShouldBeGreaterThan(0);
        await using OpenIdentityStackDbContext read = fixture.CreateDbContext();
        (await read.UpstreamProviders.SingleAsync(p => p.Id == providerId)).TrustEmailVerification.ShouldBeTrue();
        (await read.Users.CountAsync(user => user.EmailVerificationEvidence.Any(e => e.ProviderId == providerId.Value && e.WithdrawnAt == null))).ShouldBe(205);
        (await read.AuditLogEntries.CountAsync(entry => entry.EntityId == providerId.Value.ToString())).ShouldBe(0);
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
        }
        await seed.SaveChangesAsync();
        return provider.Id;
    }

    private static AuditLogService Audit(OpenIdentityStackDbContext db) => new(NullLogger<AuditLogService>.Instance, db, new Clock());
    private sealed class Clock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateTimeOffset Now => DateTimeOffset.Now;
    }

    private sealed class BatchObserver : SaveChangesInterceptor
    {
        private Guid? firstUser;
        public bool FailOnSecondBatch { get; init; }
        public int Batches { get; private set; }
        public int MaximumTrackedUsers { get; private set; }
        public int SuccessfulUserSaves { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            User[] users = eventData.Context!.ChangeTracker.Entries<User>().Select(entry => entry.Entity).ToArray();
            this.MaximumTrackedUsers = Math.Max(this.MaximumTrackedUsers, users.Length);
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
            return ValueTask.FromResult(result);
        }
    }
}
