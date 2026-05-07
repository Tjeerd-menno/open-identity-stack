using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Sessions;
/// <summary>
/// Unit tests for Single Logout (SLO) notification logic.
/// </summary>
public sealed class SingleLogoutTests
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogoutNotifier _logoutNotifier;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ProcessLogoutUseCase _useCase;
    private static readonly DateTimeOffset TestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

    public SingleLogoutTests()
    {
        this._sessionRepository = Substitute.For<ISessionRepository>();
        this._logoutNotifier = Substitute.For<ILogoutNotifier>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(TestTime);
        this._useCase = new ProcessLogoutUseCase(
            this._sessionRepository,
            this._logoutNotifier,
            this._dateTimeProvider);
    }

    [Fact]
    public async Task ProcessLogout_RevokesSessionAndNotifiesClients()
    {
        // Arrange
        UserSession session = this.CreateSessionWithClients("client-1", "client-2", "client-3");

        this._sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);
        this._logoutNotifier.NotifyClientsAsync(
            Arg.Any<SessionId>(),
            Arg.Any<IReadOnlyList<ClientSessionInfo>>(),
            Arg.Any<CancellationToken>())
            .Returns(new LogoutNotificationResult(2, 0, []));

        // Act
        Result<ProcessLogoutResult> result = await this._useCase.ExecuteAsync(session.Id, "client-1");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session.Status.ShouldBe(SessionStatus.LoggedOut);
        await this._logoutNotifier.Received(1).NotifyClientsAsync(
            session.Id,
            Arg.Is<IReadOnlyList<ClientSessionInfo>>(c => c.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessLogout_SessionNotFound_ReturnsError()
    {
        // Arrange
        var sessionId = SessionId.Create();

        this._sessionRepository.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns((UserSession?)null);

        // Act
        Result<ProcessLogoutResult> result = await this._useCase.ExecuteAsync(sessionId, "client-1");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.Session.NotFound");
    }

    [Fact]
    public async Task ProcessLogout_NoOtherClients_SkipsNotification()
    {
        // Arrange
        UserSession session = this.CreateSessionWithClients("client-1");

        this._sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        Result<ProcessLogoutResult> result = await this._useCase.ExecuteAsync(session.Id, "client-1");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        // Should not notify if the only client is the initiator
        await this._logoutNotifier.DidNotReceive().NotifyClientsAsync(
            Arg.Any<SessionId>(),
            Arg.Any<IReadOnlyList<ClientSessionInfo>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessLogout_ExcludesInitiatingClientFromNotification()
    {
        // Arrange
        UserSession session = this.CreateSessionWithClients("client-1", "client-2", "client-3");

        this._sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);
        this._logoutNotifier.NotifyClientsAsync(
            Arg.Any<SessionId>(),
            Arg.Any<IReadOnlyList<ClientSessionInfo>>(),
            Arg.Any<CancellationToken>())
            .Returns(new LogoutNotificationResult(2, 0, []));

        // Act
        Result<ProcessLogoutResult> result = await this._useCase.ExecuteAsync(session.Id, "client-1");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await this._logoutNotifier.Received(1).NotifyClientsAsync(
            session.Id,
            Arg.Is<IReadOnlyList<ClientSessionInfo>>(c => 
                c.All(client => client.ClientId != "client-1")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessLogout_NotificationFailures_ReturnsPartialSuccess()
    {
        // Arrange
        UserSession session = this.CreateSessionWithClients("client-1", "client-2", "client-3");

        this._sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);
        this._logoutNotifier.NotifyClientsAsync(
            Arg.Any<SessionId>(),
            Arg.Any<IReadOnlyList<ClientSessionInfo>>(),
            Arg.Any<CancellationToken>())
            .Returns(new LogoutNotificationResult(1, 1, ["client-3"]));

        // Act
        Result<ProcessLogoutResult> result = await this._useCase.ExecuteAsync(session.Id, "client-1");

        // Assert - Logout still succeeds even with partial notification failures
        result.IsSuccess.ShouldBeTrue();
        result.Value.NotificationResult.SuccessCount.ShouldBe(1);
        result.Value.NotificationResult.FailureCount.ShouldBe(1);
        result.Value.NotificationResult.FailedClients.ShouldContain("client-3");
    }

    [Fact]
    public async Task ProcessLogout_AlreadyRevokedSession_ReturnsError()
    {
        // Arrange
        UserSession session = this.CreateSessionWithClients("client-1");
        session.Revoke(this._dateTimeProvider);

        this._sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        Result<ProcessLogoutResult> result = await this._useCase.ExecuteAsync(session.Id, "client-1");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.Session.AlreadyRevoked");
    }

    private UserSession CreateSessionWithClients(params string[] clientIds)
    {
        UserSession session = UserSession.Create(
            UserId.Create(),
            "192.168.1.100",
            "Mozilla/5.0",
            this._dateTimeProvider).Value;

        foreach (string clientId in clientIds)
        {
            session.AddClientSession(clientId, this._dateTimeProvider);
        }

        return session;
    }
}
