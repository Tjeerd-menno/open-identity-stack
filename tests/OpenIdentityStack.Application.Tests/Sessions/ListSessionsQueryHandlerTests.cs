using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;

namespace OpenIdentityStack.Application.Tests.Sessions;

public sealed class ListSessionsQueryHandlerTests
{
    private readonly ISessionRepository sessionRepository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly ListSessionsQueryHandler handler;
    private readonly DateTimeOffset now = new(2026, 5, 20, 10, 0, 0, TimeSpan.Zero);

    public ListSessionsQueryHandlerTests()
    {
        this.sessionRepository = Substitute.For<ISessionRepository>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(this.now);
        this.handler = new ListSessionsQueryHandler(this.sessionRepository);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsMappedSessionsAndPaginationMetadata()
    {
        UserSession first = this.CreateSession("203.0.113.10", "Browser A");
        UserSession second = this.CreateSession("203.0.113.11", "Browser B");
        first.AddClientSession("client-a", this.dateTimeProvider);
        first.AddClientSession("client-b", this.dateTimeProvider);
        var query = new ListSessionsQuery(Page: 2, PageSize: 2);
        this.sessionRepository.ListAsync(2, 2, null, null, Arg.Any<CancellationToken>())
            .Returns(([first, second], 5));

        Result<ListSessionsResult> result = await this.handler.ExecuteAsync(query);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
        result.Value.TotalCount.ShouldBe(5);
        result.Value.Page.ShouldBe(2);
        result.Value.PageSize.ShouldBe(2);
        result.Value.TotalPages.ShouldBe(3);
        result.Value.HasNextPage.ShouldBeTrue();
        result.Value.HasPreviousPage.ShouldBeTrue();
        result.Value.Items[0].Id.ShouldBe(first.Id);
        result.Value.Items[0].UserId.ShouldBe(first.UserId);
        result.Value.Items[0].IpAddress.ShouldBe(first.IpAddress);
        result.Value.Items[0].UserAgent.ShouldBe(first.UserAgent);
        result.Value.Items[0].Status.ShouldBe(SessionStatus.Active);
        result.Value.Items[0].ClientCount.ShouldBe(2);
        result.Value.Items[0].LastActivityAt.ShouldBe(first.LastActivityAt);
        result.Value.Items[0].ExpiresAt.ShouldBe(first.ExpiresAt);
        result.Value.Items[0].CreatedAt.ShouldBe(first.CreatedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WithFilters_PassesFiltersToRepository()
    {
        var userId = UserId.Create();
        var query = new ListSessionsQuery(
            Page: 3,
            PageSize: 10,
            UserId: userId,
            Status: SessionStatus.Revoked);
        using var cts = new CancellationTokenSource();
        this.sessionRepository.ListAsync(3, 10, userId, SessionStatus.Revoked, cts.Token)
            .Returns(([], 0));

        Result<ListSessionsResult> result = await this.handler.ExecuteAsync(query, cts.Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
        await this.sessionRepository.Received(1).ListAsync(3, 10, userId, SessionStatus.Revoked, cts.Token);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTotalCountIsZero_ReturnsNoNextOrPreviousPage()
    {
        this.sessionRepository.ListAsync(1, 20, null, null, Arg.Any<CancellationToken>())
            .Returns(([], 0));

        Result<ListSessionsResult> result = await this.handler.ExecuteAsync(new ListSessionsQuery());

        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalPages.ShouldBe(0);
        result.Value.HasNextPage.ShouldBeFalse();
        result.Value.HasPreviousPage.ShouldBeFalse();
    }

    private UserSession CreateSession(string ipAddress, string userAgent)
    {
        return UserSession.Create(
            UserId.Create(),
            ipAddress,
            userAgent,
            this.dateTimeProvider).Value;
    }
}
