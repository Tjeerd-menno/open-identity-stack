using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Application.Tests.Sessions;

public sealed class RevokeAllUserSessionsUseCaseTests
{
    private readonly ISessionRepository sessionRepository;
    private readonly IUserRepository userRepository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly RevokeAllUserSessionsUseCase useCase;
    private readonly DateTimeOffset now = new(2026, 5, 20, 10, 0, 0, TimeSpan.Zero);

    public RevokeAllUserSessionsUseCaseTests()
    {
        this.sessionRepository = Substitute.For<ISessionRepository>();
        this.userRepository = Substitute.For<IUserRepository>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(this.now);
        this.useCase = new RevokeAllUserSessionsUseCase(
            this.sessionRepository,
            this.userRepository,
            this.dateTimeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotExist_ReturnsNotFoundAndDoesNotLoadSessions()
    {
        var userId = UserId.Create();
        this.userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        Result<RevokeAllUserSessionsResult> result = await this.useCase.ExecuteAsync(
            new RevokeAllUserSessionsCommand(userId));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UserErrors.NotFound.Code);
        await this.sessionRepository.DidNotReceive().GetActiveByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RevokesEveryActiveSession()
    {
        User user = this.CreateUser("user@example.com");
        UserSession first = this.CreateSession(user.Id);
        UserSession second = this.CreateSession(user.Id);
        this.userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        this.sessionRepository.GetActiveByUserIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([first, second]);

        Result<RevokeAllUserSessionsResult> result = await this.useCase.ExecuteAsync(
            new RevokeAllUserSessionsCommand(user.Id));

        result.IsSuccess.ShouldBeTrue();
        result.Value.RevokedCount.ShouldBe(2);
        result.Value.RevokedAt.ShouldBe(this.now);
        first.Status.ShouldBe(SessionStatus.Revoked);
        second.Status.ShouldBe(SessionStatus.Revoked);
        await this.sessionRepository.Received(1).UpdateAsync(first, Arg.Any<CancellationToken>());
        await this.sessionRepository.Received(1).UpdateAsync(second, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenExcludeSessionIdIsProvided_LeavesThatSessionActive()
    {
        User user = this.CreateUser("user@example.com");
        UserSession currentSession = this.CreateSession(user.Id);
        UserSession otherSession = this.CreateSession(user.Id);
        this.userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        this.sessionRepository.GetActiveByUserIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([currentSession, otherSession]);

        Result<RevokeAllUserSessionsResult> result = await this.useCase.ExecuteAsync(
            new RevokeAllUserSessionsCommand(user.Id, currentSession.Id));

        result.IsSuccess.ShouldBeTrue();
        result.Value.RevokedCount.ShouldBe(1);
        currentSession.Status.ShouldBe(SessionStatus.Active);
        otherSession.Status.ShouldBe(SessionStatus.Revoked);
        await this.sessionRepository.DidNotReceive().UpdateAsync(currentSession, Arg.Any<CancellationToken>());
        await this.sessionRepository.Received(1).UpdateAsync(otherSession, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoActiveSessions_ReturnsZeroRevoked()
    {
        User user = this.CreateUser("user@example.com");
        this.userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        this.sessionRepository.GetActiveByUserIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([]);

        Result<RevokeAllUserSessionsResult> result = await this.useCase.ExecuteAsync(
            new RevokeAllUserSessionsCommand(user.Id));

        result.IsSuccess.ShouldBeTrue();
        result.Value.RevokedCount.ShouldBe(0);
        result.Value.RevokedAt.ShouldBe(this.now);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationToken()
    {
        User user = this.CreateUser("user@example.com");
        UserSession session = this.CreateSession(user.Id);
        using var cts = new CancellationTokenSource();
        this.userRepository.GetByIdAsync(user.Id, cts.Token).Returns(user);
        this.sessionRepository.GetActiveByUserIdAsync(user.Id, cts.Token).Returns([session]);

        await this.useCase.ExecuteAsync(new RevokeAllUserSessionsCommand(user.Id), cts.Token);

        await this.userRepository.Received(1).GetByIdAsync(user.Id, cts.Token);
        await this.sessionRepository.Received(1).GetActiveByUserIdAsync(user.Id, cts.Token);
        await this.sessionRepository.Received(1).UpdateAsync(session, cts.Token);
    }

    private User CreateUser(string email)
    {
        return User.CreateLocal(email, "Test User", "hashed-password", this.dateTimeProvider).Value;
    }

    private UserSession CreateSession(UserId userId)
    {
        return UserSession.Create(
            userId,
            "203.0.113.10",
            "Mozilla/5.0",
            this.dateTimeProvider).Value;
    }
}
