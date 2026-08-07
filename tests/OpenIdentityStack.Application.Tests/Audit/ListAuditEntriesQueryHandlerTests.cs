using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Audit.Queries;

using SharedKernel;

namespace OpenIdentityStack.Application.Tests.Audit;

public sealed class ListAuditEntriesQueryHandlerTests
{
    private readonly IAuditEntryReader auditEntryReader;
    private readonly ListAuditEntriesQueryHandler handler;

    public ListAuditEntriesQueryHandlerTests()
    {
        this.auditEntryReader = Substitute.For<IAuditEntryReader>();
        this.handler = new ListAuditEntriesQueryHandler(this.auditEntryReader);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsMappedAuditEntriesAndPaginationMetadata()
    {
        var entryId = Guid.NewGuid();
        DateTimeOffset timestamp = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var item = new AuditEntryListItem(
            entryId,
            timestamp,
            "admin-user",
            "Application.Updated",
            "Application",
            "orders-web",
            "Changed display name",
            "{\"displayName\":\"Old\"}",
            "{\"displayName\":\"New\"}");
        var query = new ListAuditEntriesQuery(Page: 2, PageSize: 1);
        this.auditEntryReader.ListAsync(query, Arg.Any<CancellationToken>())
            .Returns(new ListAuditEntriesResult([item], TotalCount: 3, Page: 2, PageSize: 1));

        Result<ListAuditEntriesResult> result = await this.handler.ExecuteAsync(query);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(1);
        result.Value.Items[0].Id.ShouldBe(entryId);
        result.Value.Items[0].Timestamp.ShouldBe(timestamp);
        result.Value.Items[0].UserId.ShouldBe("admin-user");
        result.Value.Items[0].Action.ShouldBe("Application.Updated");
        result.Value.Items[0].EntityType.ShouldBe("Application");
        result.Value.Items[0].EntityId.ShouldBe("orders-web");
        result.Value.Items[0].Details.ShouldBe("Changed display name");
        result.Value.Items[0].BeforeState.ShouldBe("{\"displayName\":\"Old\"}");
        result.Value.Items[0].AfterState.ShouldBe("{\"displayName\":\"New\"}");
        result.Value.TotalCount.ShouldBe(3);
        result.Value.TotalPages.ShouldBe(3);
        result.Value.HasNextPage.ShouldBeTrue();
        result.Value.HasPreviousPage.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_NormalizesPagingAndPassesFiltersToReader()
    {
        DateTimeOffset from = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var query = new ListAuditEntriesQuery(
            Page: -5,
            PageSize: 500,
            From: from,
            To: to,
            UserId: "admin-user",
            Action: "Application.Created",
            EntityType: "Application",
            EntityId: "orders-web",
            Search: "created");
        using var cts = new CancellationTokenSource();
        this.auditEntryReader.ListAsync(Arg.Any<ListAuditEntriesQuery>(), cts.Token)
            .Returns(call =>
            {
                ListAuditEntriesQuery normalized = call.Arg<ListAuditEntriesQuery>()!;
                normalized.Page.ShouldBe(1);
                normalized.PageSize.ShouldBe(100);
                normalized.From.ShouldBe(from);
                normalized.To.ShouldBe(to);
                normalized.UserId.ShouldBe("admin-user");
                normalized.Action.ShouldBe("Application.Created");
                normalized.EntityType.ShouldBe("Application");
                normalized.EntityId.ShouldBe("orders-web");
                normalized.Search.ShouldBe("created");
                return new ListAuditEntriesResult([], TotalCount: 0, Page: 1, PageSize: 100);
            });

        Result<ListAuditEntriesResult> result = await this.handler.ExecuteAsync(query, cts.Token);

        result.IsSuccess.ShouldBeTrue();
        await this.auditEntryReader.Received(1).ListAsync(Arg.Any<ListAuditEntriesQuery>(), cts.Token);
    }
}
