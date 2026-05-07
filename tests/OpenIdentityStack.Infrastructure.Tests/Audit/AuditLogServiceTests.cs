using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Tests.Common;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Tests.Audit;

/// <summary>
/// Unit tests for the AuditLogService.
/// </summary>
public sealed class AuditLogServiceTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset TestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private readonly SqliteTestFixture fixture;
    private readonly ILogger<AuditLogService> logger;
    private readonly IDateTimeProvider dateTimeProvider;
    private OpenIdentityStackDbContext dbContext = null!;
    private AuditLogService auditLogService = null!;

    public AuditLogServiceTests(SqliteTestFixture fixture)
    {
        this.fixture = fixture;
        this.logger = Substitute.For<ILogger<AuditLogService>>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(TestTime);
    }

    public async ValueTask InitializeAsync()
    {
        await this.fixture.ClearAllDataAsync();
        this.dbContext = this.fixture.CreateDbContext();
        this.auditLogService = new AuditLogService(this.logger, this.dbContext, this.dateTimeProvider);
    }

    public async ValueTask DisposeAsync()
    {
        await this.dbContext.DisposeAsync();
    }

    [Fact]
    public async Task LogAsync_WithAllParameters_LogsMessageAndPersistsEntry()
    {
        await this.auditLogService.LogAsync("user-123", "User.Login", "User", "entity-456", "Login from IP 192.168.1.1");

        this.logger.ReceivedCalls().ShouldNotBeEmpty();

        await using OpenIdentityStackDbContext freshContext = this.fixture.CreateDbContext();
        AuditLogEntry? entry = await freshContext.AuditLogEntries.SingleAsync();
        entry.UserId.ShouldBe("user-123");
        entry.Action.ShouldBe("User.Login");
        entry.EntityType.ShouldBe("User");
        entry.EntityId.ShouldBe("entity-456");
        entry.Details.ShouldBe("Login from IP 192.168.1.1");
        entry.Timestamp.ShouldBe(TestTime);
    }

    [Fact]
    public async Task LogChangeAsync_WithAllParameters_LogsMessageAndPersistsEntry()
    {
        await this.auditLogService.LogChangeAsync(
            "user-123",
            "User.Updated",
            "User",
            "entity-456",
            "{\"name\":\"Old Name\"}",
            "{\"name\":\"New Name\"}");

        this.logger.ReceivedCalls().ShouldNotBeEmpty();

        await using OpenIdentityStackDbContext freshContext = this.fixture.CreateDbContext();
        AuditLogEntry? entry = await freshContext.AuditLogEntries.SingleAsync();
        entry.BeforeState.ShouldBe("{\"name\":\"Old Name\"}");
        entry.AfterState.ShouldBe("{\"name\":\"New Name\"}");
        entry.Timestamp.ShouldBe(TestTime);
    }

    [Fact]
    public async Task LogAsync_UsesDateTimeProviderForTimestamp()
    {
        await this.auditLogService.LogAsync("user-123", "User.Login", "User", "entity-456");

        _ = this.dateTimeProvider.Received().UtcNow;
    }

    [Fact]
    public async Task LogAsync_CompletesSuccessfully()
    {
        Task task = this.auditLogService.LogAsync("user-123", "User.Login", "User", "entity-456");

        await task;
        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task LogChangeAsync_CompletesSuccessfully()
    {
        Task task = this.auditLogService.LogChangeAsync("user-123", "User.Updated", "User", "entity-456", "before", "after");

        await task;
        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task LogAsync_WithCanceledToken_ThrowsAndDoesNotPersist()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            this.auditLogService.LogAsync("user-123", "User.Login", "User", "entity-456", cancellationToken: cts.Token));

        await using OpenIdentityStackDbContext freshContext = this.fixture.CreateDbContext();
        (await freshContext.AuditLogEntries.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task LogChangeAsync_WithCanceledToken_ThrowsAndDoesNotPersist()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            this.auditLogService.LogChangeAsync("user-123", "User.Updated", "User", "entity-456", "before", "after", cts.Token));

        await using OpenIdentityStackDbContext freshContext = this.fixture.CreateDbContext();
        (await freshContext.AuditLogEntries.CountAsync()).ShouldBe(0);
    }
}
