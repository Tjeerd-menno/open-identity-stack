using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class EmailVerificationEvidencePersistenceTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    [Fact]
    public async Task IndependentEvidenceRoundTripsWhileBootstrapRemainsUnverified()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 9, 6, 9, 0, 0, TimeSpan.Zero));
        User verified = User.CreateLocal($"verified-{Guid.NewGuid():N}@example.com", "Verified", "hash", clock).Value;
        verified.VerifyEmail(clock).IsSuccess.ShouldBeTrue();
        User bootstrap = User.CreateBootstrap($"bootstrap-{Guid.NewGuid():N}@example.com", "Bootstrap", "hash", clock).Value;

        await using (OpenIdentityStackDbContext write = fixture.CreateDbContext())
        {
            write.Users.AddRange(verified, bootstrap);
            await write.SaveChangesAsync();
        }

        await using OpenIdentityStackDbContext read = fixture.CreateDbContext();
        User persistedVerified = await read.Users.Include(user => user.EmailVerificationEvidence).SingleAsync(user => user.Id == verified.Id);
        User persistedBootstrap = await read.Users.Include(user => user.EmailVerificationEvidence).SingleAsync(user => user.Id == bootstrap.Id);
        persistedVerified.EmailVerified.ShouldBeTrue();
        persistedVerified.EmailVerificationEvidence.ShouldHaveSingleItem().ProviderId.ShouldBeNull();
        persistedBootstrap.Status.ShouldBe(UserStatus.Active);
        persistedBootstrap.EmailVerified.ShouldBeFalse();
        persistedBootstrap.EmailVerificationEvidence.ShouldBeEmpty();
    }
}
