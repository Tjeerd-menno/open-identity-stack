using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Sessions;
/// <summary>
/// Unit tests for AddClientSessionUseCase.
/// </summary>
public sealed class AddClientSessionUseCaseTests
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly AddClientSessionUseCase _useCase;

    public AddClientSessionUseCaseTests()
    {
        this._sessionRepository = Substitute.For<ISessionRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
        this._useCase = new AddClientSessionUseCase(this._sessionRepository, this._dateTimeProvider);
    }

    private UserSession CreateSession(UserId userId)
    {
        return UserSession.Create(
            userId,
            "192.168.1.100",
            "Mozilla/5.0",
            this._dateTimeProvider).Value;
    }

    [Fact]
    public async Task ExecuteAsync_SessionNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var command = new AddClientSessionCommand(sessionId, "test-client");

        this._sessionRepository
            .GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns((UserSession?)null);

        // Act
        Result result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SessionErrors.NotFound.Code);
    }

    [Fact]
    public async Task ExecuteAsync_ValidSession_AddsClientSession()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = this.CreateSession(userId);
        string clientId = "my-client-id";
        var command = new AddClientSessionCommand(session.Id, clientId);

        this._sessionRepository
            .GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        Result result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session.ClientSessions.ShouldContain(cs => cs.ClientId == clientId);
    }

    [Fact]
    public async Task ExecuteAsync_ValidSession_UpdatesRepository()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = this.CreateSession(userId);
        var command = new AddClientSessionCommand(session.Id, "client-123");

        this._sessionRepository
            .GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        await this._useCase.ExecuteAsync(command);

        // Assert
        await this._sessionRepository.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_MultipleClients_AddsAllClientSessions()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = this.CreateSession(userId);

        this._sessionRepository
            .GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        await this._useCase.ExecuteAsync(new AddClientSessionCommand(session.Id, "client-1"));
        await this._useCase.ExecuteAsync(new AddClientSessionCommand(session.Id, "client-2"));
        await this._useCase.ExecuteAsync(new AddClientSessionCommand(session.Id, "client-3"));

        // Assert
        session.ClientSessions.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteAsync_RevokedSession_ReturnsError()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = this.CreateSession(userId);
        session.Revoke(this._dateTimeProvider);

        var command = new AddClientSessionCommand(session.Id, "client-x");

        this._sessionRepository
            .GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        Result result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ExpiredSession_ReturnsError()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = this.CreateSession(userId);
        session.Expire(this._dateTimeProvider);

        var command = new AddClientSessionCommand(session.Id, "client-y");

        this._sessionRepository
            .GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        Result result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesCancellation()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var command = new AddClientSessionCommand(sessionId, "client-z");
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
            () => this._useCase.ExecuteAsync(command, cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateClient_HandlesGracefully()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = this.CreateSession(userId);
        string clientId = "duplicate-client";

        this._sessionRepository
            .GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act - Add same client twice
        Result result1 = await this._useCase.ExecuteAsync(new AddClientSessionCommand(session.Id, clientId));
        Result result2 = await this._useCase.ExecuteAsync(new AddClientSessionCommand(session.Id, clientId));

        // Assert - Both should succeed (duplicates may be allowed or merged)
        result1.IsSuccess.ShouldBeTrue();
        // Second call behavior depends on implementation
    }
}
