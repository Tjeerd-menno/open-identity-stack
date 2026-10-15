using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Sessions;

public sealed class GetSessionQueryHandlerTests
{
    private readonly ISessionRepository sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly GetSessionQueryHandler handler;

    public GetSessionQueryHandlerTests()
    {
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero));
        this.handler = new GetSessionQueryHandler(this.sessionRepository);
    }

    [Fact]
    public async Task HandleAsync_WhenSessionIsMissing_ReturnsNull()
    {
        var sessionId = SessionId.Create();
        this.sessionRepository.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns((UserSession?)null);

        GetSessionResult? result = await this.handler.HandleAsync(new GetSessionQuery(sessionId));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenSessionExists_ReturnsSessionFields()
    {
        var userId = UserId.Create();
        UserSession session = UserSession.Create(userId, "192.0.2.10", "TestAgent", this.dateTimeProvider).Value;
        session.AddClientSession("management-web-client", this.dateTimeProvider);
        this.sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        GetSessionResult? result = await this.handler.HandleAsync(new GetSessionQuery(session.Id));

        result.ShouldNotBeNull();
        result.Id.ShouldBe(session.Id);
        result.UserId.ShouldBe(userId);
        result.IpAddress.ShouldBe("192.0.2.10");
        result.UserAgent.ShouldBe("TestAgent");
        result.Status.ShouldBe(SessionStatus.Active);
        result.ClientCount.ShouldBe(1);
        result.LastActivityAt.ShouldBe(session.LastActivityAt);
        result.ExpiresAt.ShouldBe(session.ExpiresAt);
        result.CreatedAt.ShouldBe(session.CreatedAt);
    }
}
