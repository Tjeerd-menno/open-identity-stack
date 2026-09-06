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

public sealed class ProviderEmailIdempotencyTests(FederationPolicyTestFixture fixture) : IClassFixture<FederationPolicyTestFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentSourceEvidenceIsIdempotentAndRetainsPendingEdits(bool alreadyBound)
    {
        UpstreamProvider provider = UpstreamProvider.Create($"provider-{Guid.NewGuid():N}", "Provider", "https://issuer.example", "client").Value;
        provider.SetEmailVerificationTrust(true);
        if (alreadyBound) { provider.BindIssuer(provider.Authority, provider.Authority); }
        User user = User.ProvisionFederated($"{Guid.NewGuid():N}@example.com", "User", provider.Id, provider.Name, "subject", provider.Authority).Value;
        await using (OpenIdentityStackDbContext seed = fixture.CreateDbContext())
        {
            seed.Add(provider); seed.Add(user);
            await seed.SaveChangesAsync();
        }
        await using OpenIdentityStackDbContext first = fixture.CreateDbContext();
        await using OpenIdentityStackDbContext second = fixture.CreateDbContext();
        UpstreamProvider firstProvider = await first.UpstreamProviders.SingleAsync(p => p.Id == provider.Id);
        UpstreamProvider secondProvider = await second.UpstreamProviders.SingleAsync(p => p.Id == provider.Id);
        User firstUser = await first.Users.SingleAsync(u => u.Id == user.Id);
        User secondUser = await second.Users.SingleAsync(u => u.Id == user.Id);
        firstProvider.BindIssuer(provider.Authority, provider.Authority).IsSuccess.ShouldBeTrue();
        secondProvider.BindIssuer(provider.Authority, provider.Authority).IsSuccess.ShouldBeTrue();
        firstUser.RecordProviderEmailVerification(firstProvider, provider.Authority, firstUser.Email, true, DateTimeOffset.UtcNow);
        secondUser.RecordProviderEmailVerification(secondProvider, provider.Authority, secondUser.Email, true, DateTimeOffset.UtcNow);
        secondUser.UpdateDisplayName("Pending user edit", new Clock()).IsSuccess.ShouldBeTrue();
        secondProvider.UpdateDisplayName("Pending provider edit").IsSuccess.ShouldBeTrue();

        Task<Result> firstCommit = new JitProvisioningPersistence(first, Audit(first)).CommitAsync(user.Id, provider.Id, false);
        Result[] results;
        if (fixture.IsPostgres)
        {
            results = await Task.WhenAll(firstCommit, new JitProvisioningPersistence(second, Audit(second)).CommitAsync(user.Id, provider.Id, false));
        }
        else
        {
            results = [await firstCommit, await new JitProvisioningPersistence(second, Audit(second)).CommitAsync(user.Id, provider.Id, false)];
        }
        results.ShouldAllBe(result => result.IsSuccess);
        await using OpenIdentityStackDbContext read = fixture.CreateDbContext();
        User persisted = await read.Users.SingleAsync(u => u.Id == user.Id);
        persisted.EmailVerificationEvidence.Count.ShouldBe(1);
        (await read.AuditLogEntries.CountAsync(entry => entry.Action == "Federation.EmailVerificationEvidenceRecorded"
            && entry.EntityId == user.Id.Value.ToString())).ShouldBe(1);
        persisted.DisplayName.ShouldBe("Pending user edit");
        firstUser.EmailEvidenceRevision.ShouldBe(persisted.EmailEvidenceRevision);
        secondUser.EmailEvidenceRevision.ShouldBe(persisted.EmailEvidenceRevision);
        firstUser.CredentialRevision.ShouldBe(persisted.CredentialRevision);
        secondUser.CredentialRevision.ShouldBe(persisted.CredentialRevision);
        UpstreamProvider persistedProvider = await read.UpstreamProviders.SingleAsync(p => p.Id == provider.Id);
        persistedProvider.DisplayName.ShouldBe("Pending provider edit");
        persistedProvider.BoundIssuer.ShouldBe(provider.Authority);
        persistedProvider.IdentityConfigurationLocked.ShouldBeTrue();
        firstUser.EmailVerificationEvidence.Single().Id.ShouldBe(persisted.EmailVerificationEvidence.Single().Id);
        secondUser.EmailVerificationEvidence.Single().Id.ShouldBe(persisted.EmailVerificationEvidence.Single().Id);
    }

    [Fact]
    public async Task EvidenceAuditFailureRollsBackProofAndFirstIssuerBinding()
    {
        UpstreamProvider provider = UpstreamProvider.Create($"audit-{Guid.NewGuid():N}", "Provider", "https://issuer.example", "client").Value;
        provider.SetEmailVerificationTrust(true);
        User user = User.ProvisionFederated($"{Guid.NewGuid():N}@example.com", "User", provider.Id, provider.Name, "subject", provider.Authority).Value;
        await using (OpenIdentityStackDbContext seed = fixture.CreateDbContext())
        {
            seed.AddRange(provider, user);
            await seed.SaveChangesAsync();
        }
        await using OpenIdentityStackDbContext writer = fixture.CreateDbContext();
        UpstreamProvider trackedProvider = await writer.UpstreamProviders.SingleAsync(value => value.Id == provider.Id);
        User trackedUser = await writer.Users.SingleAsync(value => value.Id == user.Id);
        trackedProvider.BindIssuer(provider.Authority, provider.Authority);
        trackedUser.RecordProviderEmailVerification(trackedProvider, provider.Authority, user.Email, true, DateTimeOffset.UtcNow);
        IAuditLog audit = Substitute.For<IAuditLog>();
        audit.LogAsync("federation", "Federation.EmailVerificationEvidenceRecorded", "User", user.Id.Value.ToString(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                // Persist the audit too, then fail before the transaction can commit.
                await Audit(writer).LogAsync(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<string>(2), call.ArgAt<string>(3), call.ArgAt<string?>(4), call.ArgAt<CancellationToken>(5));
                throw new InvalidOperationException("Injected evidence-audit failure.");
            });

        await Should.ThrowAsync<InvalidOperationException>(() => new JitProvisioningPersistence(writer, audit).CommitAsync(user.Id, provider.Id, false));

        writer.ChangeTracker.Entries().ShouldBeEmpty();
        await using OpenIdentityStackDbContext read = fixture.CreateDbContext();
        (await read.Users.SingleAsync(value => value.Id == user.Id)).EmailVerificationEvidence.ShouldBeEmpty();
        (await read.UpstreamProviders.SingleAsync(value => value.Id == provider.Id)).BoundIssuer.ShouldBeNull();
        (await read.AuditLogEntries.AnyAsync(value => value.EntityId == user.Id.Value.ToString())).ShouldBeFalse();
    }

    private static AuditLogService Audit(OpenIdentityStackDbContext db) => new(NullLogger<AuditLogService>.Instance, db, new Clock());
    private sealed class Clock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateTimeOffset Now => DateTimeOffset.Now;
    }
}
