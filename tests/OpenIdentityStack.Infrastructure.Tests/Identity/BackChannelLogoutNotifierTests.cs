
using System.Net;
using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Infrastructure.Identity;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Tests.Identity;
/// <summary>
/// Unit tests for BackChannelLogoutNotifier.
/// </summary>
public sealed class BackChannelLogoutNotifierTests
{
    private readonly ILogger<BackChannelLogoutNotifier> _logger;
    private readonly ILogoutTokenFactory _logoutTokenFactory;

    public BackChannelLogoutNotifierTests()
    {
        this._logger = Substitute.For<ILogger<BackChannelLogoutNotifier>>();
        this._logoutTokenFactory = Substitute.For<ILogoutTokenFactory>();
        this._logoutTokenFactory.CreateLogoutToken(Arg.Any<SessionId>(), Arg.Any<string>())
            .Returns("header.payload.signature");
    }

    [Fact]
    public async Task NotifyClientsAsync_ClientWithoutBackChannelUri_SkipsClient()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var notifier = new BackChannelLogoutNotifier(httpClient, this._logoutTokenFactory, this._logger);

        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client-1", "https://client1.com/logout", null) // No back-channel URI
        };

        // Act
        LogoutNotificationResult result = await notifier.NotifyClientsAsync(sessionId, clients);

        // Assert
        result.SuccessCount.ShouldBe(0);
        result.FailureCount.ShouldBe(0);
        result.FailedClients.ShouldBeEmpty();
    }

    [Fact]
    public async Task NotifyClientsAsync_SuccessfulNotification_IncrementsSuccessCount()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var notifier = new BackChannelLogoutNotifier(httpClient, this._logoutTokenFactory, this._logger);

        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client-1", null, "https://client1.com/backchannel-logout")
        };

        // Act
        LogoutNotificationResult result = await notifier.NotifyClientsAsync(sessionId, clients);

        // Assert
        result.SuccessCount.ShouldBe(1);
        result.FailureCount.ShouldBe(0);
        result.FailedClients.ShouldBeEmpty();
    }

    [Fact]
    public async Task NotifyClientsAsync_FailedHttpResponse_AddsToFailedClients()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var httpClient = new HttpClient(handler);
        var notifier = new BackChannelLogoutNotifier(httpClient, this._logoutTokenFactory, this._logger);

        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client-1", null, "https://client1.com/backchannel-logout")
        };

        // Act
        LogoutNotificationResult result = await notifier.NotifyClientsAsync(sessionId, clients);

        // Assert
        result.SuccessCount.ShouldBe(0);
        result.FailureCount.ShouldBe(1);
        result.FailedClients.ShouldContain("client-1");
    }

    [Fact]
    public async Task NotifyClientsAsync_HttpException_AddsToFailedClients()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(_ => throw new HttpRequestException("Network error"));
        var httpClient = new HttpClient(handler);
        var notifier = new BackChannelLogoutNotifier(httpClient, this._logoutTokenFactory, this._logger);

        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client-1", null, "https://client1.com/backchannel-logout")
        };

        // Act
        LogoutNotificationResult result = await notifier.NotifyClientsAsync(sessionId, clients);

        // Assert
        result.SuccessCount.ShouldBe(0);
        result.FailureCount.ShouldBe(1);
        result.FailedClients.ShouldContain("client-1");
    }

    [Fact]
    public async Task NotifyClientsAsync_MultipleClients_ProcessesAllClients()
    {
        // Arrange
        int callCount = 0;
        var handler = new MockHttpMessageHandler(_ =>
        {
            callCount++;
            return callCount == 2
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);
        var notifier = new BackChannelLogoutNotifier(httpClient, this._logoutTokenFactory, this._logger);

        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client-1", null, "https://client1.com/logout"),
            new("client-2", null, "https://client2.com/logout"),
            new("client-3", null, "https://client3.com/logout")
        };

        // Act
        LogoutNotificationResult result = await notifier.NotifyClientsAsync(sessionId, clients);

        // Assert
        result.SuccessCount.ShouldBe(2);
        result.FailureCount.ShouldBe(1);
        result.FailedClients.ShouldContain("client-2");
    }

    [Fact]
    public async Task NotifyClientsAsync_EmptyClientList_ReturnsZeroCounts()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var notifier = new BackChannelLogoutNotifier(httpClient, this._logoutTokenFactory, this._logger);

        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>();

        // Act
        LogoutNotificationResult result = await notifier.NotifyClientsAsync(sessionId, clients);

        // Assert
        result.SuccessCount.ShouldBe(0);
        result.FailureCount.ShouldBe(0);
        result.FailedClients.ShouldBeEmpty();
    }

    [Fact]
    public void GenerateFrontChannelLogoutUrls_ClientWithFrontChannelUri_GeneratesUrl()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var notifier = new BackChannelLogoutNotifier(httpClient, this._logoutTokenFactory, this._logger);

        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client-1", "https://client1.com/logout", null)
        };

        // Act
        IReadOnlyList<string> urls = notifier.GenerateFrontChannelLogoutUrls(sessionId, clients);

        // Assert
        urls.ShouldHaveSingleItem();
        urls[0].ShouldContain("sid=");
        urls[0].ShouldStartWith("https://client1.com/logout");
    }

    [Fact]
    public void GenerateFrontChannelLogoutUrls_UriWithExistingQueryParams_UsesAmpersand()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var notifier = new BackChannelLogoutNotifier(httpClient, this._logoutTokenFactory, this._logger);

        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client-1", "https://client1.com/logout?param=value", null)
        };

        // Act
        IReadOnlyList<string> urls = notifier.GenerateFrontChannelLogoutUrls(sessionId, clients);

        // Assert
        urls.ShouldHaveSingleItem();
        urls[0].ShouldContain("&sid=");
    }

    [Fact]
    public void GenerateFrontChannelLogoutUrls_UriWithoutQueryParams_UsesQuestionMark()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var notifier = new BackChannelLogoutNotifier(httpClient, this._logoutTokenFactory, this._logger);

        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client-1", "https://client1.com/logout", null)
        };

        // Act
        IReadOnlyList<string> urls = notifier.GenerateFrontChannelLogoutUrls(sessionId, clients);

        // Assert
        urls.ShouldHaveSingleItem();
        urls[0].ShouldContain("?sid=");
    }

    [Fact]
    public void GenerateFrontChannelLogoutUrls_NoFrontChannelUri_ReturnsEmpty()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var notifier = new BackChannelLogoutNotifier(httpClient, this._logoutTokenFactory, this._logger);

        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client-1", null, "https://client1.com/backchannel")
        };

        // Act
        IReadOnlyList<string> urls = notifier.GenerateFrontChannelLogoutUrls(sessionId, clients);

        // Assert
        urls.ShouldBeEmpty();
    }

    [Fact]
    public async Task NotifyClientsAsync_PostsTheSignedLogoutTokenAsFormContent()
    {
        // Arrange
        // Read the body inside the handler: HttpClient disposes the request once the call
        // completes, so the content is not readable afterwards. ReadAsStream is the synchronous
        // API, which keeps this off the blocking-task-in-a-test path.
        string? postedBody = null;
        var handler = new MockHttpMessageHandler(request =>
        {
            using var reader = new StreamReader(request.Content!.ReadAsStream());
            postedBody = reader.ReadToEnd();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);
        this._logoutTokenFactory.CreateLogoutToken(Arg.Any<SessionId>(), "client-1")
            .Returns("signed.logout.token");
        var notifier = new BackChannelLogoutNotifier(httpClient, this._logoutTokenFactory, this._logger);

        var clients = new List<ClientSessionInfo>
        {
            new("client-1", null, "https://client1.com/backchannel")
        };

        // Act
        LogoutNotificationResult result = await notifier.NotifyClientsAsync(SessionId.Create(), clients);

        // Assert
        result.SuccessCount.ShouldBe(1);
        postedBody.ShouldBe("logout_token=signed.logout.token");
    }

    [Fact]
    public async Task NotifyClientsAsync_WhenTokenCannotBeSigned_FailsWithoutNotifyingTheClient()
    {
        // A client must never receive an unsigned or malformed token, so a signing failure is
        // reported as a failed notification rather than degrading to an unverifiable request.
        // Arrange
        bool requestSent = false;
        var handler = new MockHttpMessageHandler(_ =>
        {
            requestSent = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);
        this._logoutTokenFactory.CreateLogoutToken(Arg.Any<SessionId>(), Arg.Any<string>())
            .Returns(_ => throw new InvalidOperationException("no signing credentials"));
        var notifier = new BackChannelLogoutNotifier(httpClient, this._logoutTokenFactory, this._logger);

        var clients = new List<ClientSessionInfo>
        {
            new("client-1", null, "https://client1.com/backchannel")
        };

        // Act
        LogoutNotificationResult result = await notifier.NotifyClientsAsync(SessionId.Create(), clients);

        // Assert
        requestSent.ShouldBeFalse();
        result.SuccessCount.ShouldBe(0);
        result.FailureCount.ShouldBe(1);
        result.FailedClients.ShouldContain("client-1");
    }

    [Fact]
    public async Task NotifyClientsAsync_WhenCallerCancels_PropagatesInsteadOfReportingFailures()
    {
        // The exception is raised inside the notifier's own try block rather than through
        // HttpClient, whose exception translation varies by runtime version; the behaviour under
        // test is the catch filter, not that translation.
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        this._logoutTokenFactory.CreateLogoutToken(Arg.Any<SessionId>(), Arg.Any<string>())
            .Returns(_ => throw new OperationCanceledException(cts.Token));
        var notifier = new BackChannelLogoutNotifier(httpClient, this._logoutTokenFactory, this._logger);

        var clients = new List<ClientSessionInfo>
        {
            new("client-1", null, "https://client1.com/backchannel"),
            new("client-2", null, "https://client2.com/backchannel")
        };

        // Act / Assert
        // Swallowing this would report client-2 as a failed notification when it was never
        // attempted, so cancellation has to surface to the caller.
        await Should.ThrowAsync<OperationCanceledException>(
            () => notifier.NotifyClientsAsync(SessionId.Create(), clients, cts.Token));
    }

    [Fact]
    public async Task NotifyClientsAsync_WhenATimeoutCancelsWithoutTheCallerAsking_RecordsAFailure()
    {
        // A transport timeout surfaces as OperationCanceledException carrying its own token, not
        // the caller's. That is a per-client failure and must not abort the remaining clients.
        // Arrange
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        this._logoutTokenFactory.CreateLogoutToken(Arg.Any<SessionId>(), "client-1")
            .Returns(_ => throw new TaskCanceledException(
                "The request timed out.", null, new CancellationToken(canceled: true)));
        this._logoutTokenFactory.CreateLogoutToken(Arg.Any<SessionId>(), "client-2")
            .Returns("signed.logout.token");
        var notifier = new BackChannelLogoutNotifier(httpClient, this._logoutTokenFactory, this._logger);

        var clients = new List<ClientSessionInfo>
        {
            new("client-1", null, "https://client1.com/backchannel"),
            new("client-2", null, "https://client2.com/backchannel")
        };

        // Act
        LogoutNotificationResult result = await notifier.NotifyClientsAsync(
            SessionId.Create(), clients, CancellationToken.None);

        // Assert
        result.FailedClients.ShouldContain("client-1");
        result.SuccessCount.ShouldBe(1);
        result.FailureCount.ShouldBe(1);
    }

    /// <summary>
    /// Mock HTTP message handler for testing HttpClient.
    /// </summary>
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            this._handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(this._handler(request));
        }
    }
}
