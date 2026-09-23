using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Infrastructure.Identity;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Tests.Common;

namespace OpenIdentityStack.Infrastructure.Tests.Identity;

public sealed class ConsentApprovalTransactionRunnerTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture fixture;

    public ConsentApprovalTransactionRunnerTests(SqliteTestFixture fixture) => this.fixture = fixture;

    public async ValueTask InitializeAsync() => await this.fixture.ClearAllDataAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ExecuteAsync_WhenAuditPersistenceFails_RollsBackPriorAuthorizationWrite()
    {
        await using OpenIdentityStackDbContext context = this.fixture.CreateDbContext();
        var runner = new ConsentApprovalTransactionRunner(context);
        var approval = new AuditLogEntry
        {
            UserId = "subject-1",
            Action = "Consent.Approved",
            EntityType = "Application",
            EntityId = "client-1",
            Details = "approval",
            Timestamp = DateTimeOffset.UtcNow
        };

        await Should.ThrowAsync<InvalidOperationException>(() => runner.ExecuteAsync<bool>(async cancellationToken =>
        {
            context.AuditLogEntries.Add(approval);
            await context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Audit persistence failed after authorization was saved.");
        }));

        context.ChangeTracker.Clear();
        (await context.AuditLogEntries.CountAsync()).ShouldBe(0);
    }
}
