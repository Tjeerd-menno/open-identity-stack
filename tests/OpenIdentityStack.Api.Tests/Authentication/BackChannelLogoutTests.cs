using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIdentityStack.Api.Authentication;
using OpenIdentityStack.Api.Tests.Helpers;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Api.Tests.Authentication;

public class BackChannelLogoutTests
{
    private readonly IProcessLogoutUseCase _processLogoutUseCase;
    private readonly IFrontChannelLogoutService _frontChannelLogoutService;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogoutNotifier _logoutNotifier;
    private readonly IOpenIddictRequestService _requestService;
    private readonly LogoutController _controller;

    public BackChannelLogoutTests()
    {
        this._processLogoutUseCase = Substitute.For<IProcessLogoutUseCase>();
        this._frontChannelLogoutService = Substitute.For<IFrontChannelLogoutService>();
        this._sessionRepository = Substitute.For<ISessionRepository>();
        this._logoutNotifier = Substitute.For<ILogoutNotifier>();
        this._requestService = Substitute.For<IOpenIddictRequestService>();

        this._controller = new LogoutController(
            this._processLogoutUseCase,
            this._frontChannelLogoutService,
            this._sessionRepository,
            this._logoutNotifier,
            this._requestService
        );

        DefaultHttpContext httpContext = HttpContextTestHelper.CreateWithAuthenticationServices();
        this._controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task Logout_TriggersProcessLogoutUseCase()
    {
        // Arrange
        var sessionId = SessionId.Create();
        Claim[] claims = new[] { new Claim("session_id", sessionId.Value.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        this._controller.ControllerContext.HttpContext.User = principal;

        var request = new OpenIddictRequest();
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        var logoutResult = new ProcessLogoutResult(
            SessionId: sessionId,
            NotificationResult: new LogoutNotificationResult(0, 0, Array.Empty<string>()),
            FrontChannelLogoutUrls: Array.Empty<string>()
        );

        this._processLogoutUseCase.ExecuteAsync(Arg.Any<SessionId>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((Result<ProcessLogoutResult>)logoutResult);

        // Act
        await this._controller.Logout();

        // Assert
        await this._processLogoutUseCase.Received(1).ExecuteAsync(
            Arg.Is<SessionId>(id => id == sessionId),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_WhenProcessFails_ReturnsBadRequest()
    {
        // Arrange
        var sessionId = SessionId.Create();
        Claim[] claims = new[] { new Claim("session_id", sessionId.Value.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        this._controller.ControllerContext.HttpContext.User = principal;

        var request = new OpenIddictRequest();
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        this._processLogoutUseCase.ExecuteAsync(Arg.Any<SessionId>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((Result<ProcessLogoutResult>)DomainError.Validation("Logout.Failed", "Logout failed"));

        // Act
        IActionResult result = await this._controller.Logout();

        // Assert
        BadRequestObjectResult badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        LogoutResponse response = Assert.IsType<LogoutResponse>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Logout failed", response.Message);
    }

    [Fact]
    public async Task Logout_WithBackChannelClients_NotifiesViaBackChannel()
    {
        // Arrange
        var sessionId = SessionId.Create();
        Claim[] claims = new[] { new Claim("session_id", sessionId.Value.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        this._controller.ControllerContext.HttpContext.User = principal;

        var request = new OpenIddictRequest
        {
            PostLogoutRedirectUri = "https://home.com"
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        var backChannelNotificationResult = new LogoutNotificationResult(2, 0, Array.Empty<string>());
        var logoutResult = new ProcessLogoutResult(
            SessionId: sessionId,
            NotificationResult: backChannelNotificationResult,
            FrontChannelLogoutUrls: Array.Empty<string>()
        );

        this._processLogoutUseCase.ExecuteAsync(Arg.Any<SessionId>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((Result<ProcessLogoutResult>)logoutResult);

        // Act
        IActionResult result = await this._controller.Logout();

        // Assert - verifies the use case was called which internally triggers back-channel notifications
        await this._processLogoutUseCase.Received(1).ExecuteAsync(
            Arg.Any<SessionId>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_WithPartialBackChannelFailures_StillSucceeds()
    {
        // Arrange
        var sessionId = SessionId.Create();
        Claim[] claims = new[] { new Claim("session_id", sessionId.Value.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        this._controller.ControllerContext.HttpContext.User = principal;

        var request = new OpenIddictRequest
        {
            PostLogoutRedirectUri = "https://home.com"
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        // Some back-channel notifications failed but logout still succeeds
        var failedClients = new List<string> { "failed-client" };
        var backChannelNotificationResult = new LogoutNotificationResult(1, 1, failedClients);
        var logoutResult = new ProcessLogoutResult(
            SessionId: sessionId,
            NotificationResult: backChannelNotificationResult,
            FrontChannelLogoutUrls: Array.Empty<string>()
        );

        this._processLogoutUseCase.ExecuteAsync(Arg.Any<SessionId>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((Result<ProcessLogoutResult>)logoutResult);

        // Act
        IActionResult result = await this._controller.Logout();

        // Assert - logout succeeds even with partial failures (per SLO spec)
        // Controller now uses explicit SignOutAsync before redirecting, so returns RedirectResult
        RedirectResult redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("https://home.com", redirect.Url);
    }
}
