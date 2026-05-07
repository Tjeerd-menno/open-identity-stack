using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Sessions;
/// <summary>
/// Unit tests for ValidateSessionQueryHandler.
/// </summary>
public sealed class ValidateSessionQueryHandlerTests
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ValidateSessionQueryHandler _handler;

    public ValidateSessionQueryHandlerTests()
    {
        this._sessionRepository = Substitute.For<ISessionRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
        this._handler = new ValidateSessionQueryHandler(this._sessionRepository, this._dateTimeProvider);
    }

    private UserSession CreateSession(UserId userId)
    {
        // Use _dateTimeProvider to avoid CA1822 warning
        return UserSession.Create(
            userId,
            "192.168.1.100",
            "Mozilla/5.0",
            this._dateTimeProvider).Value;
    }

    private static UserSession CreateSession(UserId userId, IDateTimeProvider provider)
    {
        return UserSession.Create(
            userId,
            "192.168.1.100",
            "Mozilla/5.0",
            provider).Value;
    }

    [Fact]
    public async Task HandleAsync_SessionNotFound_ReturnsInvalid()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var query = new ValidateSessionQuery(sessionId);

        this._sessionRepository
            .GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns((UserSession?)null);

        // Act
        ValidateSessionResult result = await this._handler.HandleAsync(query);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Reason.ShouldBe("Session not found");
    }

    [Fact]
    public async Task HandleAsync_RevokedSession_ReturnsInvalid()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = this.CreateSession(userId);
        session.Revoke(this._dateTimeProvider);

        var query = new ValidateSessionQuery(session.Id);

        this._sessionRepository
            .GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        ValidateSessionResult result = await this._handler.HandleAsync(query);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Reason.ShouldBe("Session has been revoked");
    }

    [Fact]
    public async Task HandleAsync_ExpiredSession_ReturnsInvalid()
    {
        // Arrange
        var userId = UserId.Create();

        // Create session 25 hours ago (default duration is 24 hours, so it's expired)
        IDateTimeProvider pastDateTimeProvider = Substitute.For<IDateTimeProvider>();
        pastDateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow.AddHours(-25));
        UserSession session = CreateSession(userId, pastDateTimeProvider);

        // Current time is now (session was created 25 hours ago, expired 1 hour ago)
        this._dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);

        var query = new ValidateSessionQuery(session.Id);

        this._sessionRepository
            .GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        ValidateSessionResult result = await this._handler.HandleAsync(query);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Reason!.ShouldContain("expired");
    }

    [Fact]
    public async Task HandleAsync_ActiveSession_ReturnsValid()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = this.CreateSession(userId);

        var query = new ValidateSessionQuery(session.Id);

        this._sessionRepository
            .GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        ValidateSessionResult result = await this._handler.HandleAsync(query);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Reason.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_ValidSession_UpdatesLastActivity()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = this.CreateSession(userId);

        var query = new ValidateSessionQuery(session.Id);

        this._sessionRepository
            .GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        await this._handler.HandleAsync(query);

        // Assert
        await this._sessionRepository.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_InvalidSession_DoesNotUpdateRepository()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var query = new ValidateSessionQuery(sessionId);

        this._sessionRepository
            .GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns((UserSession?)null);

        // Act
        await this._handler.HandleAsync(query);

        // Assert
        await this._sessionRepository.DidNotReceive().UpdateAsync(Arg.Any<UserSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SessionWithExpiredStatus_ReturnsInvalid()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = this.CreateSession(userId);

        // Force expire the session
        session.Expire(this._dateTimeProvider);

        var query = new ValidateSessionQuery(session.Id);

        this._sessionRepository
            .GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        ValidateSessionResult result = await this._handler.HandleAsync(query);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Reason!.ShouldContain("expired");
    }

    [Fact]
    public async Task HandleAsync_PropagatesCancellation()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var query = new ValidateSessionQuery(sessionId);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        this._sessionRepository
            .GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return Task.FromResult<UserSession?>(null);
            });

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => this._handler.HandleAsync(query, cts.Token));
    }
}
