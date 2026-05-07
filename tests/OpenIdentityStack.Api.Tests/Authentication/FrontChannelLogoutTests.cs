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

public class FrontChannelLogoutTests
{
    private readonly IProcessLogoutUseCase _processLogoutUseCase;
    private readonly IFrontChannelLogoutService _frontChannelLogoutService;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogoutNotifier _logoutNotifier;
    private readonly IOpenIddictRequestService _requestService;
    private readonly LogoutController _controller;

    public FrontChannelLogoutTests()
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
    public async Task Logout_WithFrontChannelClients_ReturnsOkWithIframes()
    {
        // Arrange
        var sessionId = SessionId.Create();
        Claim[] claims = new[] { new Claim("session_id", sessionId.Value.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        this._controller.ControllerContext.HttpContext.User = principal;

        // No PostLogoutRedirectUri to get OkObjectResult with iframes (with redirect URI it redirects immediately)
        var request = new OpenIddictRequest();
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        string[] logoutUrls = new[] { "https://client1.com/logout", "https://client2.com/logout" };
        var logoutResult = new ProcessLogoutResult(
            SessionId: sessionId,
            NotificationResult: new LogoutNotificationResult(0, 0, Array.Empty<string>()),
            FrontChannelLogoutUrls: logoutUrls
        );

        this._processLogoutUseCase.ExecuteAsync(Arg.Any<SessionId>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((Result<ProcessLogoutResult>)logoutResult);

        // Act
        IActionResult result = await this._controller.Logout();

        // Assert
        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        LogoutResponse response = Assert.IsType<LogoutResponse>(okResult.Value);
        
        Assert.True(response.Success);
        Assert.Equal(2, response.FrontChannelLogoutFrames.Count);
        Assert.Contains(response.FrontChannelLogoutFrames, iframe => iframe.Url == "https://client1.com/logout");
        Assert.Contains(response.FrontChannelLogoutFrames, iframe => iframe.Url == "https://client2.com/logout");
    }

    [Fact]
    public async Task Logout_WithNoFrontChannelClients_RedirectsImmediately()
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

        var logoutResult = new ProcessLogoutResult(
            SessionId: sessionId,
            NotificationResult: new LogoutNotificationResult(0, 0, Array.Empty<string>()),
            FrontChannelLogoutUrls: Array.Empty<string>()
        );

        this._processLogoutUseCase.ExecuteAsync(Arg.Any<SessionId>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((Result<ProcessLogoutResult>)logoutResult);

        // Act
        IActionResult result = await this._controller.Logout();

        // Assert - Controller now uses explicit SignOutAsync before redirecting, so returns RedirectResult
        RedirectResult redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("https://home.com", redirect.Url);
    }

    [Fact]
    public async Task Logout_WithNoSession_RedirectsToPostLogoutUri()
    {
        // Arrange - no session_id claim set, controller returns null for GetCurrentSessionId()
        var request = new OpenIddictRequest
        {
            PostLogoutRedirectUri = "https://home.com"
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        // Act
        IActionResult result = await this._controller.Logout();

        // Assert - Controller now uses explicit SignOutAsync before redirecting, so returns RedirectResult
        RedirectResult redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("https://home.com", redirect.Url);
    }

    [Fact]
    public async Task Logout_WithNoSessionAndNoRedirectUri_ReturnsOkWithNoSession()
    {
        // Arrange - no session_id claim, no redirect URI
        var request = new OpenIddictRequest();
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        // Act
        IActionResult result = await this._controller.Logout();

        // Assert
        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        LogoutResponse response = Assert.IsType<LogoutResponse>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("No active session", response.Message);
    }
}
