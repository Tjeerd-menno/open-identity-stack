using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Federation;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class ProviderEmailEvidenceTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    [Fact]
    public async Task CommittedTrustWithdrawalRejectsEvidenceFromStaleTrustedProvider()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        UpstreamProvider provider = UpstreamProvider.Create($"provider-{Guid.NewGuid():N}", "Provider", "https://issuer.example", "client").Value;
        provider.SetEmailVerificationTrust(true);
        User user = User.CreateFederated($"{Guid.NewGuid():N}@example.com", "Person", clock).Value;
        await using (OpenIdentityStackDbContext seed = fixture.CreateDbContext())
        {
            seed.Add(provider);
            seed.Add(user);
            await seed.SaveChangesAsync();
        }

        await using OpenIdentityStackDbContext stale = fixture.CreateDbContext();
        UpstreamProvider staleProvider = await stale.UpstreamProviders.SingleAsync(p => p.Id == provider.Id);
        User staleUser = await stale.Users.SingleAsync(u => u.Id == user.Id);
        await using (OpenIdentityStackDbContext withdrawal = fixture.CreateDbContext())
        {
            var store = new ProviderEmailTrustStore(withdrawal, Substitute.For<IAuditLog>());
            (await store.SetAsync(provider.Id, false, "operator", default)).IsSuccess.ShouldBeTrue();
        }

        staleUser.RecordProviderEmailVerification(staleProvider, "https://issuer.example", staleUser.Email, true, clock.UtcNow);
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        await using OpenIdentityStackDbContext read = fixture.CreateDbContext();
        (await read.Users.SingleAsync(u => u.Id == user.Id)).EmailVerified.ShouldBeFalse();
    }
}
