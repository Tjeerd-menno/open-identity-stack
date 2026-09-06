using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Federation;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class ProviderEmailEvidenceTests(FederationPolicyTestFixture fixture) : IClassFixture<FederationPolicyTestFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommittedTrustWithdrawalRejectsEvidenceFromStaleTrustedProvider(bool newUser)
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        UpstreamProvider provider = UpstreamProvider.Create($"provider-{Guid.NewGuid():N}", "Provider", "https://issuer.example", "client").Value;
        provider.SetEmailVerificationTrust(true);
        User user = User.CreateFederated($"{Guid.NewGuid():N}@example.com", "Person", clock).Value;
        await using (OpenIdentityStackDbContext seed = fixture.CreateDbContext())
        {
            seed.Add(provider);
            if (!newUser) { seed.Add(user); }
            await seed.SaveChangesAsync();
        }

        await using OpenIdentityStackDbContext stale = fixture.CreateDbContext();
        UpstreamProvider staleProvider = await stale.UpstreamProviders.SingleAsync(p => p.Id == provider.Id);
        User staleUser = newUser ? user : await stale.Users.SingleAsync(u => u.Id == user.Id);
        if (newUser) { stale.Add(staleUser); }
        await using (OpenIdentityStackDbContext withdrawal = fixture.CreateDbContext())
        {
            var store = new ProviderEmailTrustStore(withdrawal, Substitute.For<IAuditLog>());
            (await store.SetAsync(provider.Id, false, "operator", default)).IsSuccess.ShouldBeTrue();
        }

        staleUser.RecordProviderEmailVerification(staleProvider, "https://issuer.example", staleUser.Email, true, clock.UtcNow);
        (await new JitProvisioningPersistence(stale, Audit(stale)).CommitAsync(staleUser.Id, provider.Id, newUser)).IsFailure.ShouldBeTrue();
        await using OpenIdentityStackDbContext read = fixture.CreateDbContext();
        if (newUser) { (await read.Users.AnyAsync(u => u.Id == user.Id)).ShouldBeFalse(); }
        else { (await read.Users.SingleAsync(u => u.Id == user.Id)).EmailVerified.ShouldBeFalse(); }
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OverlappingVerifiedLoginsForDifferentUsersBothSucceed(bool evidenceExists, bool newUsers)
    {
        UpstreamProvider provider = await SeedProviderAsync();
        User first = User.CreateFederated($"{Guid.NewGuid():N}@example.com", "First", new Clock()).Value;
        User second = User.CreateFederated($"{Guid.NewGuid():N}@example.com", "Second", new Clock()).Value;
        if (evidenceExists)
        {
            first.RecordProviderEmailVerification(provider, provider.BoundIssuer, first.Email, true, DateTimeOffset.UtcNow);
            second.RecordProviderEmailVerification(provider, provider.BoundIssuer, second.Email, true, DateTimeOffset.UtcNow);
        }
        if (!newUsers)
        {
            await using OpenIdentityStackDbContext seed = fixture.CreateDbContext();
            seed.AddRange(first, second);
            await seed.SaveChangesAsync();
        }
        await using OpenIdentityStackDbContext firstLogin = fixture.CreateDbContext();
        await using OpenIdentityStackDbContext secondLogin = fixture.CreateDbContext();
        UpstreamProvider firstProvider = await firstLogin.UpstreamProviders.SingleAsync(p => p.Id == provider.Id);
        UpstreamProvider secondProvider = await secondLogin.UpstreamProviders.SingleAsync(p => p.Id == provider.Id);
        first = newUsers ? first : await firstLogin.Users.SingleAsync(u => u.Id == first.Id);
        second = newUsers ? second : await secondLogin.Users.SingleAsync(u => u.Id == second.Id);
        first.RecordProviderEmailVerification(firstProvider, provider.BoundIssuer, first.Email, true, DateTimeOffset.UtcNow);
        second.RecordProviderEmailVerification(secondProvider, provider.BoundIssuer, second.Email, true, DateTimeOffset.UtcNow);
        if (newUsers) { firstLogin.Add(first); secondLogin.Add(second); }
        // Both login requests have read the same policy snapshot before either commits.
        Task<Result> firstCommit = new JitProvisioningPersistence(firstLogin, Audit(firstLogin)).CommitAsync(first.Id, provider.Id, newUsers);
        Result firstResult;
        Result secondResult;
        if (fixture.IsPostgres)
        {
            Task<Result> secondCommit = new JitProvisioningPersistence(secondLogin, Audit(secondLogin)).CommitAsync(second.Id, provider.Id, newUsers);
            Result[] results = await Task.WhenAll(firstCommit, secondCommit);
            firstResult = results[0]; secondResult = results[1];
        }
        else
        {
            firstResult = await firstCommit;
            secondResult = await new JitProvisioningPersistence(secondLogin, Audit(secondLogin)).CommitAsync(second.Id, provider.Id, newUsers);
        }
        firstResult.IsSuccess.ShouldBeTrue();
        secondResult.IsSuccess.ShouldBeTrue();
        await using OpenIdentityStackDbContext read = fixture.CreateDbContext();
        (await read.Users.SingleAsync(u => u.Id == first.Id)).EmailVerified.ShouldBeTrue();
        (await read.Users.SingleAsync(u => u.Id == second.Id)).EmailVerified.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommittedProvisioningPolicyDeniesStaleNewAccount(bool disabled)
    {
        UpstreamProvider provider = await SeedProviderAsync();
        await using OpenIdentityStackDbContext login = fixture.CreateDbContext();
        UpstreamProvider staleProvider = await login.UpstreamProviders.SingleAsync(p => p.Id == provider.Id);
        User user = User.CreateFederated($"{Guid.NewGuid():N}@example.com", "User", new Clock()).Value;
        login.Add(user);
        await using (OpenIdentityStackDbContext policy = fixture.CreateDbContext())
        {
            UpstreamProvider current = await policy.UpstreamProviders.SingleAsync(p => p.Id == provider.Id);
            if (disabled) { current.Disable(); } else { current.SetJitProvisioningEnabled(false); }
            await policy.SaveChangesAsync();
        }
        (await new JitProvisioningPersistence(login, Audit(login)).CommitAsync(user.Id, staleProvider.Id, true)).IsFailure.ShouldBeTrue();
        await using OpenIdentityStackDbContext read = fixture.CreateDbContext();
        (await read.Users.AnyAsync(u => u.Id == user.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task WithdrawalWaitsForEvidenceCommitAndThenInvalidatesIt()
    {
        Assert.SkipWhen(!fixture.IsPostgres, "Overlapping row-lock verification requires PostgreSQL.");
        UpstreamProvider provider = await SeedProviderAsync();
        await using OpenIdentityStackDbContext login = fixture.CreateDbContext();
        UpstreamProvider loginProvider = await login.UpstreamProviders.SingleAsync(p => p.Id == provider.Id);
        User user = User.CreateFederated($"{Guid.NewGuid():N}@example.com", "User", new Clock()).Value;
        user.RecordProviderEmailVerification(loginProvider, provider.BoundIssuer, user.Email, true, DateTimeOffset.UtcNow);
        login.Add(user);
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IAuditLog audit = Substitute.For<IAuditLog>();
        audit.LogAsync(Arg.Any<string>(), "Federation.NewAccountAssociationRecorded", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(async _ => { saved.TrySetResult(); await release.Task; });
        Task<Result> creation = new JitProvisioningPersistence(login, audit).CommitAsync(user.Id, provider.Id, true);
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using OpenIdentityStackDbContext withdrawal = fixture.CreateDbContext();
        Task<Result>? withdrawing = null;
        try
        {
            withdrawing = new ProviderEmailTrustStore(withdrawal, Audit(withdrawal)).SetAsync(provider.Id, false, "operator", default);
            await using OpenIdentityStackDbContext observer = fixture.CreateDbContext();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!await observer.Database.SqlQueryRaw<bool>("""
                SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = current_database()
                    AND wait_event_type = 'Lock' AND query LIKE '%upstream_providers%') AS "Value"
                """).SingleAsync(deadline.Token))
            {
                await Task.Delay(20, deadline.Token);
            }
        }
        finally { release.TrySetResult(); }
        (await creation).IsSuccess.ShouldBeTrue();
        (await withdrawing!).IsSuccess.ShouldBeTrue();
        await using OpenIdentityStackDbContext read = fixture.CreateDbContext();
        (await read.Users.SingleAsync(u => u.Id == user.Id)).EmailVerified.ShouldBeFalse();
    }

    private async Task<UpstreamProvider> SeedProviderAsync()
    {
        UpstreamProvider provider = UpstreamProvider.Create($"provider-{Guid.NewGuid():N}", "Provider", "https://issuer.example", "client").Value;
        provider.SetEmailVerificationTrust(true);
        provider.BindIssuer("https://issuer.example", provider.Authority);
        await using OpenIdentityStackDbContext seed = fixture.CreateDbContext();
        seed.Add(provider);
        await seed.SaveChangesAsync();
        return provider;
    }

    private static AuditLogService Audit(OpenIdentityStackDbContext db) => new(NullLogger<AuditLogService>.Instance, db, new Clock());
    private sealed class Clock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateTimeOffset Now => DateTimeOffset.Now;
    }

}
