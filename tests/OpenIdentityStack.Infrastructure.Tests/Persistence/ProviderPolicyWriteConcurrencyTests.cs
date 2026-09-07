using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Federation.Commands;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Federation;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class ProviderPolicyWriteConcurrencyTests(ProviderPolicyWriteTestFixture fixture) : IClassFixture<ProviderPolicyWriteTestFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleNoOpPolicyPatchConflictsAndRetryAuditsActualTransition(bool initiallyEnabled)
    {
        UpstreamProvider provider = UpstreamProvider.Create($"policy-{Guid.NewGuid():N}", "Provider", "https://issuer.example", "client").Value;
        provider.SetJitProvisioningEnabled(initiallyEnabled);
        await using (OpenIdentityStackDbContext seed = fixture.CreateDbContext())
        {
            seed.Add(provider);
            await seed.SaveChangesAsync();
        }
        await using OpenIdentityStackDbContext stale = fixture.CreateDbContext();
        await using OpenIdentityStackDbContext winner = fixture.CreateDbContext();
        var realRepository = new UpstreamProviderRepository(stale);
        IUpstreamProviderRepository interleaved = Substitute.For<IUpstreamProviderRepository>();
        interleaved.GetByIdAsync(provider.Id, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            UpstreamProvider? cached = await realRepository.GetByIdAsync(provider.Id);
            (await UseCase(winner).ExecuteAsync(new(provider.Id.Value, JitProvisioningEnabled: !initiallyEnabled, ActorId: "winner"))).IsSuccess.ShouldBeTrue();
            return cached;
        });
        interleaved.When(value => value.RequireProvisioningPolicyWrite(Arg.Any<UpstreamProvider>())).Do(call =>
        {
            UpstreamProvider tracked = call.Arg<UpstreamProvider>();
            realRepository.RequireProvisioningPolicyWrite(tracked);
            // Exercise a pure no-op write: checking the policy must not depend on UpdatedAt changing.
            stale.Entry(tracked).Property(value => value.UpdatedAt).CurrentValue = stale.Entry(tracked).Property(value => value.UpdatedAt).OriginalValue;
        });
        interleaved.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(call => realRepository.SaveChangesAsync(call.Arg<CancellationToken>()));
        var staleUseCase = new UpdateProviderUseCase(interleaved, audit: Audit(stale));

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => staleUseCase.ExecuteAsync(new(provider.Id.Value, JitProvisioningEnabled: initiallyEnabled, ActorId: "stale")));

        await using OpenIdentityStackDbContext read = fixture.CreateDbContext();
        (await read.UpstreamProviders.SingleAsync(value => value.Id == provider.Id)).JitProvisioningEnabled.ShouldBe(!initiallyEnabled);
        AuditLogEntry winnerAudit = await read.AuditLogEntries.SingleAsync(value => value.EntityId == provider.Id.Value.ToString());
        winnerAudit.UserId.ShouldBe("winner");
        winnerAudit.BeforeState.ShouldBe(State(initiallyEnabled));
        winnerAudit.AfterState.ShouldBe(State(!initiallyEnabled));
        (await UseCase(read).ExecuteAsync(new(provider.Id.Value, JitProvisioningEnabled: initiallyEnabled, ActorId: "retry"))).IsSuccess.ShouldBeTrue();
        await using OpenIdentityStackDbContext verify = fixture.CreateDbContext();
        (await verify.UpstreamProviders.SingleAsync(value => value.Id == provider.Id)).JitProvisioningEnabled.ShouldBe(initiallyEnabled);
        AuditLogEntry retryAudit = await verify.AuditLogEntries.SingleAsync(value => value.EntityId == provider.Id.Value.ToString() && value.UserId == "retry");
        retryAudit.BeforeState.ShouldBe(State(!initiallyEnabled));
        retryAudit.AfterState.ShouldBe(State(initiallyEnabled));
    }

    private static string State(bool enabled) => enabled ? "{\"jitProvisioningEnabled\":true}" : "{\"jitProvisioningEnabled\":false}";
    private static UpdateProviderUseCase UseCase(OpenIdentityStackDbContext db) => new(new UpstreamProviderRepository(db), audit: Audit(db));
    private static AuditLogService Audit(OpenIdentityStackDbContext db) => new(NullLogger<AuditLogService>.Instance, db, new Clock());
    private sealed class Clock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateTimeOffset Now => DateTimeOffset.Now;
    }
}
