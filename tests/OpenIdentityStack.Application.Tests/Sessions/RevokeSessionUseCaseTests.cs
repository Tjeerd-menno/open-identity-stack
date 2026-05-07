using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Sessions;
/// <summary>
/// Unit tests for the RevokeSessionUseCase.
/// </summary>
public sealed class RevokeSessionUseCaseTests
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly RevokeSessionUseCase _useCase;
    private static readonly DateTimeOffset TestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

    public RevokeSessionUseCaseTests()
    {
        this._sessionRepository = Substitute.For<ISessionRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(TestTime);
        this._useCase = new RevokeSessionUseCase(this._sessionRepository, this._dateTimeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidSession_RevokesSession()
    {
        // Arrange
        UserSession session = this.CreateActiveSession();
        var command = new RevokeSessionCommand(session.Id);

        this._sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        Result<RevokeSessionResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session.Status.ShouldBe(SessionStatus.Revoked);
        await this._sessionRepository.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SessionNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var command = new RevokeSessionCommand(sessionId);

        this._sessionRepository.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns((UserSession?)null);

        // Act
        Result<RevokeSessionResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.Session.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyRevokedSession_ReturnsError()
    {
        // Arrange
        UserSession session = this.CreateActiveSession();
        session.Revoke(this._dateTimeProvider);

        var command = new RevokeSessionCommand(session.Id);

        this._sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        Result<RevokeSessionResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.Session.AlreadyRevoked");
    }

    [Fact]
    public async Task ExecuteAsync_ExpiredSession_ReturnsError()
    {
        // Arrange
        UserSession session = this.CreateActiveSession();
        session.Expire(this._dateTimeProvider);

        var command = new RevokeSessionCommand(session.Id);

        this._sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        Result<RevokeSessionResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.Session.AlreadyExpired");
    }

    [Fact]
    public async Task ExecuteAsync_SetsRevokedTimestamp()
    {
        // Arrange
        UserSession session = this.CreateActiveSession();
        var command = new RevokeSessionCommand(session.Id);
        DateTimeOffset revokeTime = TestTime.AddMinutes(30);
        this._dateTimeProvider.UtcNow.Returns(revokeTime);

        this._sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        Result<RevokeSessionResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session.RevokedAt.ShouldBe(revokeTime);
    }

    private UserSession CreateActiveSession()
    {
        return UserSession.Create(
            UserId.Create(),
            "192.168.1.100",
            "Mozilla/5.0",
            this._dateTimeProvider).Value;
    }
}
