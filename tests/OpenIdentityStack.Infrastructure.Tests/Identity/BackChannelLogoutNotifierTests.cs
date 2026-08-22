
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
        string? postedBody = null;
        var handler = new MockHttpMessageHandler(request =>
        {
            postedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
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
