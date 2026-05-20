using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Application.Tests.Sessions;

public sealed class CreateSessionUseCaseTests
{
    private readonly ISessionRepository sessionRepository;
    private readonly IUserRepository userRepository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly CreateSessionUseCase useCase;
    private readonly DateTimeOffset now = new(2026, 5, 20, 10, 0, 0, TimeSpan.Zero);

    public CreateSessionUseCaseTests()
    {
        this.sessionRepository = Substitute.For<ISessionRepository>();
        this.userRepository = Substitute.For<IUserRepository>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(this.now);
        this.useCase = new CreateSessionUseCase(
            this.sessionRepository,
            this.userRepository,
            this.dateTimeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserExists_CreatesAndPersistsSession()
    {
        User user = this.CreateUser("user@example.com");
        var command = new CreateSessionCommand(
            user.Id,
            "203.0.113.10",
            "Mozilla/5.0",
            TimeSpan.FromHours(2));
        UserSession? addedSession = null;
        this.userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        this.sessionRepository.AddAsync(Arg.Do<UserSession>(session => addedSession = session), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        Result<CreateSessionResult> result = await this.useCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.SessionId.ShouldNotBe(SessionId.Empty);
        addedSession.ShouldNotBeNull();
        addedSession.Id.ShouldBe(result.Value.SessionId);
        addedSession.UserId.ShouldBe(user.Id);
        addedSession.IpAddress.ShouldBe(command.IpAddress);
        addedSession.UserAgent.ShouldBe(command.UserAgent);
        addedSession.CreatedAt.ShouldBe(this.now);
        addedSession.ExpiresAt.ShouldBe(this.now.AddHours(2));
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotExist_ReturnsNotFoundAndDoesNotPersist()
    {
        var userId = UserId.Create();
        var command = new CreateSessionCommand(userId, "203.0.113.10", "Mozilla/5.0");
        this.userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        Result<CreateSessionResult> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.User.NotFound");
        await this.sessionRepository.DidNotReceive().AddAsync(Arg.Any<UserSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidUserId_ReturnsValidationError()
    {
        var command = new CreateSessionCommand(UserId.Empty, "203.0.113.10", "Mozilla/5.0");
        this.userRepository.GetByIdAsync(UserId.Empty, Arg.Any<CancellationToken>())
            .Returns(this.CreateUser("user@example.com"));

        Result<CreateSessionResult> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SessionErrors.UserIdRequired.Code);
        await this.sessionRepository.DidNotReceive().AddAsync(Arg.Any<UserSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_UsesDefaultSessionDurationWhenDurationIsNotProvided()
    {
        User user = this.CreateUser("user@example.com");
        var command = new CreateSessionCommand(user.Id, "203.0.113.10", "Mozilla/5.0");
        UserSession? addedSession = null;
        this.userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        this.sessionRepository.AddAsync(Arg.Do<UserSession>(session => addedSession = session), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        Result<CreateSessionResult> result = await this.useCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        addedSession.ShouldNotBeNull();
        addedSession.ExpiresAt.ShouldBe(this.now.AddHours(24));
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationToken()
    {
        User user = this.CreateUser("user@example.com");
        var command = new CreateSessionCommand(user.Id, "203.0.113.10", "Mozilla/5.0");
        using var cts = new CancellationTokenSource();
        this.userRepository.GetByIdAsync(user.Id, cts.Token).Returns(user);

        await this.useCase.ExecuteAsync(command, cts.Token);

        await this.userRepository.Received(1).GetByIdAsync(user.Id, cts.Token);
        await this.sessionRepository.Received(1).AddAsync(Arg.Any<UserSession>(), cts.Token);
    }

    private User CreateUser(string email)
    {
        return User.CreateLocal(email, "Test User", "hashed-password", this.dateTimeProvider).Value;
    }
}
