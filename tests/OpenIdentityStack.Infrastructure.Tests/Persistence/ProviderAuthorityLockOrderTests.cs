using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Federation;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class ProviderAuthorityLockOrderTests(FederationPolicyTestFixture fixture) : IClassFixture<FederationPolicyTestFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthorityIsAcquiredBeforeProviderPolicy(bool withdrawal)
    {
        Assert.SkipWhen(!fixture.IsPostgres, "Overlapping row-lock verification requires PostgreSQL.");
        UpstreamProvider provider = UpstreamProvider.Create($"provider-{Guid.NewGuid():N}", "Provider", "https://issuer.example", "client").Value;
        provider.BindIssuer(provider.Authority, provider.Authority);
        provider.SetEmailVerificationTrust(true);
        await using (OpenIdentityStackDbContext seed = fixture.CreateDbContext())
        {
            seed.Add(provider);
            await seed.SaveChangesAsync();
        }
        await using OpenIdentityStackDbContext owner = fixture.CreateDbContext();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction heldAuthority = await owner.Database.BeginTransactionAsync();
        await owner.Database.ExecuteSqlRawAsync("""
            UPDATE "AdministrativeAuthorityRevision" SET "Revision" = "Revision" WHERE "Id" = 1
            """);
        await using OpenIdentityStackDbContext writer = fixture.CreateDbContext();
        Task<Result> operation;
        if (withdrawal)
        {
            IEmailTrustCredentialInvalidator invalidator = Substitute.For<IEmailTrustCredentialInvalidator>();
            invalidator.RevokeAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(new EmailTrustCredentialInvalidation(0, 0, 0));
            operation = new ProviderEmailTrustStore(writer, Audit(writer), invalidator).SetAsync(provider.Id, false, "operator", default);
        }
        else
        {
            UpstreamProvider loginProvider = await writer.UpstreamProviders.SingleAsync(p => p.Id == provider.Id);
            User user = User.CreateFederated($"{Guid.NewGuid():N}@example.com", "User", new Clock()).Value;
            user.RecordProviderEmailVerification(loginProvider, provider.Authority, user.Email, true, DateTimeOffset.UtcNow);
            writer.Add(user);
            operation = new JitProvisioningPersistence(writer, Audit(writer)).CommitAsync(user.Id, provider.Id, true);
        }
        try
        {
            await using OpenIdentityStackDbContext observer = fixture.CreateDbContext();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!await observer.Database.SqlQueryRaw<bool>("""
                SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = current_database()
                    AND wait_event_type = 'Lock' AND query LIKE '%AdministrativeAuthorityRevision%') AS "Value"
                """).SingleAsync(deadline.Token))
            {
                operation.IsCompleted.ShouldBeFalse("the provider operation must serialize behind the authority writer");
                await Task.Delay(20, deadline.Token);
            }
            // A writer waiting for authority must not hold the provider lock in the opposite order.
            Guid lockedProvider = await observer.Database.SqlQuery<Guid>($"""
                SELECT id AS "Value" FROM upstream_providers WHERE id = {provider.Id.Value} FOR UPDATE NOWAIT
                """).SingleAsync(deadline.Token);
            lockedProvider.ShouldBe(provider.Id.Value);
        }
        finally { await heldAuthority.RollbackAsync(); }
        (await operation).IsSuccess.ShouldBeTrue();
    }

    private static AuditLogService Audit(OpenIdentityStackDbContext db) => new(NullLogger<AuditLogService>.Instance, db, new Clock());
    private sealed class Clock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateTimeOffset Now => DateTimeOffset.Now;
    }
}
