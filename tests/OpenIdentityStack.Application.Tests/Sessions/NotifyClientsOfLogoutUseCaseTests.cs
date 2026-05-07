using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Sessions;
/// <summary>
/// Unit tests for the NotifyClientsOfLogoutUseCase.
/// </summary>
public sealed class NotifyClientsOfLogoutUseCaseTests
{
    private readonly ILogoutNotifier _logoutNotifier;
    private readonly NotifyClientsOfLogoutUseCase _useCase;

    public NotifyClientsOfLogoutUseCaseTests()
    {
        this._logoutNotifier = Substitute.For<ILogoutNotifier>();
        this._useCase = new NotifyClientsOfLogoutUseCase(this._logoutNotifier);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoClients_ReturnsEmptyResult()
    {
        // Arrange
        var sessionId = SessionId.Create();
        ClientSessionInfo[] clients = Array.Empty<ClientSessionInfo>();

        // Act
        Result<LogoutNotificationResult> result = await this._useCase.ExecuteAsync(sessionId, clients);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.SuccessCount.ShouldBe(0);
        result.Value.FailureCount.ShouldBe(0);
        result.Value.FailedClients.ShouldBeEmpty();

        // Verify notifier was NOT called
        await this._logoutNotifier.DidNotReceive().NotifyClientsAsync(
            Arg.Any<SessionId>(),
            Arg.Any<IReadOnlyList<ClientSessionInfo>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithClients_CallsNotifier()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client1", "https://client1.example.com/logout", null),
            new("client2", "https://client2.example.com/logout", null)
        };

        var expectedResult = new LogoutNotificationResult(2, 0, []);
        this._logoutNotifier.NotifyClientsAsync(sessionId, clients, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Act
        Result<LogoutNotificationResult> result = await this._useCase.ExecuteAsync(sessionId, clients);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.SuccessCount.ShouldBe(2);
        result.Value.FailureCount.ShouldBe(0);

        await this._logoutNotifier.Received(1).NotifyClientsAsync(
            sessionId,
            clients,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithPartialFailure_ReturnsFailedClients()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client1", "https://client1.example.com/logout", null),
            new("client2", "https://client2.example.com/logout", null),
            new("client3", "https://client3.example.com/logout", null)
        };

        var failedClients = new List<string> { "client2" };
        var expectedResult = new LogoutNotificationResult(2, 1, failedClients);
        this._logoutNotifier.NotifyClientsAsync(sessionId, clients, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Act
        Result<LogoutNotificationResult> result = await this._useCase.ExecuteAsync(sessionId, clients);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.SuccessCount.ShouldBe(2);
        result.Value.FailureCount.ShouldBe(1);
        result.Value.FailedClients.ShouldContain("client2");
    }

    [Fact]
    public async Task ExecuteAsync_WithAllFailures_ReturnsAllFailedClients()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client1", "https://client1.example.com/logout", null),
            new("client2", "https://client2.example.com/logout", null)
        };

        var failedClients = new List<string> { "client1", "client2" };
        var expectedResult = new LogoutNotificationResult(0, 2, failedClients);
        this._logoutNotifier.NotifyClientsAsync(sessionId, clients, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Act
        Result<LogoutNotificationResult> result = await this._useCase.ExecuteAsync(sessionId, clients);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.SuccessCount.ShouldBe(0);
        result.Value.FailureCount.ShouldBe(2);
        result.Value.FailedClients.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationToken()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client1", "https://client1.example.com/logout", null)
        };
        using var cts = new CancellationTokenSource();

        var expectedResult = new LogoutNotificationResult(1, 0, []);
        this._logoutNotifier.NotifyClientsAsync(sessionId, clients, cts.Token)
            .Returns(expectedResult);

        // Act
        Result<LogoutNotificationResult> result = await this._useCase.ExecuteAsync(sessionId, clients, cts.Token);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await this._logoutNotifier.Received(1).NotifyClientsAsync(sessionId, clients, cts.Token);
    }
}
