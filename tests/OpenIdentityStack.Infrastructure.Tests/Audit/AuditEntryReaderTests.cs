using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Audit.Queries;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Tests.Common;

namespace OpenIdentityStack.Infrastructure.Tests.Audit;

public sealed class AuditEntryReaderTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture fixture;
    private OpenIdentityStackDbContext dbContext = null!;
    private AuditEntryReader reader = null!;

    public AuditEntryReaderTests(SqliteTestFixture fixture)
    {
        this.fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        await this.fixture.ClearAllDataAsync();
        this.dbContext = this.fixture.CreateDbContext();
        this.reader = new AuditEntryReader(this.dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await this.dbContext.DisposeAsync();
    }

    [Fact]
    public async Task ListAsync_ReturnsNewestFirstPaginatedEntriesWithStateFields()
    {
        await this.SeedAsync(
            Entry("admin-a", "Application.Created", "Application", "orders-api", new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), "created", null, "{\"status\":\"Active\"}"),
            Entry("admin-b", "User.Updated", "User", "user-1", new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero), "updated", "{\"enabled\":true}", "{\"enabled\":false}"),
            Entry("admin-c", "Application.Updated", "Application", "orders-web", new DateTimeOffset(2026, 6, 1, 11, 0, 0, TimeSpan.Zero), "updated app", "{\"name\":\"Old\"}", "{\"name\":\"New\"}"));

        ListAuditEntriesResult result = await this.reader.ListAsync(new ListAuditEntriesQuery(Page: 1, PageSize: 2));

        result.TotalCount.ShouldBe(3);
        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(2);
        result.TotalPages.ShouldBe(2);
        result.Items.Select(item => item.UserId).ShouldBe(["admin-c", "admin-b"]);
        result.Items[0].Details.ShouldBe("updated app");
        result.Items[0].BeforeState.ShouldBe("{\"name\":\"Old\"}");
        result.Items[0].AfterState.ShouldBe("{\"name\":\"New\"}");
    }

    [Fact]
    public async Task ListAsync_AppliesDateIdentityActionEntityAndSearchFilters()
    {
        await this.SeedAsync(
            Entry("admin-a", "Application.Created", "Application", "orders-api", new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero), "created API", null, "{\"clientId\":\"orders-api\"}"),
            Entry("admin-b", "Application.Updated", "Application", "orders-web", new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero), "changed redirect", "{\"redirect\":\"old\"}", "{\"redirect\":\"new\"}"),
            Entry("admin-b", "Application.Updated", "Application", "billing-web", new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero), "changed billing", null, "{\"redirect\":\"billing\"}"),
            Entry("admin-b", "Role.Updated", "Role", "admin", new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero), "changed role", null, null));
        var query = new ListAuditEntriesQuery(
            Page: 1,
            PageSize: 20,
            From: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            To: new DateTimeOffset(2026, 6, 2, 23, 59, 59, TimeSpan.Zero),
            UserId: "admin-b",
            Action: "Application.Updated",
            EntityType: "Application",
            EntityId: "orders-web",
            Search: "redirect");

        ListAuditEntriesResult result = await this.reader.ListAsync(query);

        result.TotalCount.ShouldBe(1);
        result.Items.Single().EntityId.ShouldBe("orders-web");
    }

    private async Task SeedAsync(params AuditLogEntry[] entries)
    {
        this.dbContext.AuditLogEntries.AddRange(entries);
        await this.dbContext.SaveChangesAsync();
        this.dbContext.ChangeTracker.Clear();
    }

    private static AuditLogEntry Entry(
        string userId,
        string action,
        string entityType,
        string entityId,
        DateTimeOffset timestamp,
        string? details,
        string? beforeState,
        string? afterState)
    {
        return new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Timestamp = timestamp,
            Details = details,
            BeforeState = beforeState,
            AfterState = afterState
        };
    }
}
