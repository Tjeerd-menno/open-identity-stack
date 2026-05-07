
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIdentityStack.Api.Authentication;
using OpenIdentityStack.Api.Tests.Helpers;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Domain.Common;
using System.Security.Claims;

using SharedKernel;
namespace OpenIdentityStack.Api.Tests.Authentication;
public sealed class LogoutControllerTests
{
    private readonly IProcessLogoutUseCase _processLogoutUseCase;
    private readonly IFrontChannelLogoutService _frontChannelLogoutService;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogoutNotifier _logoutNotifier;
    private readonly IOpenIddictRequestService _requestService;
    private readonly LogoutController _controller;

    public LogoutControllerTests()
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
            this._requestService);

        this._controller.ControllerContext = new ControllerContext
        {
            HttpContext = HttpContextTestHelper.CreateWithAuthenticationServices()
        };
    }

    [Fact]
    public async Task Logout_ShouldRedirectWithState_WhenNoSessionAndRedirectProvided()
    {
        // Arrange
        var request = new OpenIddictRequest
        {
            PostLogoutRedirectUri = "https://example.com/logout",
            State = "abc"
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        // Act
        IActionResult result = await this._controller.Logout();

        // Assert - Controller now uses explicit SignOutAsync before redirecting, so returns RedirectResult
        RedirectResult redirect = result.ShouldBeOfType<RedirectResult>();
        redirect.Url.ShouldBe("https://example.com/logout?state=abc");
    }

    [Fact]
    public async Task Logout_ShouldReturnOk_WhenNoSessionAndNoRedirect()
    {
        // Arrange
        var request = new OpenIddictRequest();
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        // Act
        IActionResult result = await this._controller.Logout();

        // Assert
        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        LogoutResponse response = ok.Value.ShouldBeOfType<LogoutResponse>();
        response.Success.ShouldBeTrue();
        response.Message.ShouldBe("No active session");
        
        // Verify session cookie was deleted
        this._controller.HttpContext.Response.Headers.ShouldContain(h => h.Key == "Set-Cookie");
    }

    [Fact]
    public async Task Logout_ShouldReturnOk_WithFrontChannelIframes()
    {
        // Arrange
        var sessionId = SessionId.Create();
        Claim[] claims = new[] { new Claim("session_id", sessionId.Value.ToString()) };
        this._controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var request = new OpenIddictRequest();
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        var result = new ProcessLogoutResult(
            sessionId,
            new LogoutNotificationResult(1, 0, []),
            new List<string> { "https://client/logout" });

        this._processLogoutUseCase.ExecuteAsync(Arg.Any<SessionId>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((Result<ProcessLogoutResult>)result);

        // Act
        IActionResult response = await this._controller.Logout();

        // Assert
        OkObjectResult ok = response.ShouldBeOfType<OkObjectResult>();
        LogoutResponse payload = ok.Value.ShouldBeOfType<LogoutResponse>();
        payload.FrontChannelLogoutFrames.Count.ShouldBe(1);
        
        // Verify session cookie was deleted
        this._controller.HttpContext.Response.Headers.ShouldContain(h => h.Key == "Set-Cookie");
    }

    [Fact]
    public async Task Logout_ShouldRedirectWithState_WhenSuccessAndNoFrontChannelFrames()
    {
        // Arrange
        var sessionId = SessionId.Create();
        Claim[] claims = new[] { new Claim("session_id", sessionId.Value.ToString()) };
        this._controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var request = new OpenIddictRequest
        {
            PostLogoutRedirectUri = "https://example.com/logout",
            State = "s1"
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        var result = new ProcessLogoutResult(
            sessionId,
            new LogoutNotificationResult(0, 0, []),
            []);

        this._processLogoutUseCase.ExecuteAsync(Arg.Any<SessionId>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((Result<ProcessLogoutResult>)result);

        // Act
        IActionResult response = await this._controller.Logout();

        // Assert - Controller now uses explicit SignOutAsync before redirecting, so returns RedirectResult
        RedirectResult redirect = response.ShouldBeOfType<RedirectResult>();
        redirect.Url.ShouldBe("https://example.com/logout?state=s1");
    }

    [Fact]
    public async Task Logout_ShouldRedirectWithoutState_WhenStateNotProvided()
    {
        // Arrange
        var sessionId = SessionId.Create();
        Claim[] claims = new[] { new Claim("session_id", sessionId.Value.ToString()) };
        this._controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var request = new OpenIddictRequest
        {
            PostLogoutRedirectUri = "https://example.com/logout"
            // State is null/not provided
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        var result = new ProcessLogoutResult(
            sessionId,
            new LogoutNotificationResult(0, 0, []),
            []);

        this._processLogoutUseCase.ExecuteAsync(Arg.Any<SessionId>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((Result<ProcessLogoutResult>)result);

        // Act
        IActionResult response = await this._controller.Logout();

        // Assert - Controller now uses explicit SignOutAsync before redirecting, so returns RedirectResult
        RedirectResult redirect = response.ShouldBeOfType<RedirectResult>();
        redirect.Url.ShouldBe("https://example.com/logout");
    }

    [Fact]
    public async Task AdminLogout_ShouldReturnNotFound_WhenSessionMissing()
    {
        // Arrange
        this._processLogoutUseCase.ExecuteAsync(Arg.Any<SessionId>(), null, Arg.Any<CancellationToken>())
            .Returns((Result<ProcessLogoutResult>)DomainError.NotFound("Session.NotFound", "Missing"));

        // Act
        IActionResult result = await this._controller.AdminLogout(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AdminLogout_ShouldReturnOk_WhenSuccess()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var logoutResult = new ProcessLogoutResult(
            sessionId,
            new LogoutNotificationResult(0, 0, []),
            []);

        this._processLogoutUseCase.ExecuteAsync(Arg.Any<SessionId>(), null, Arg.Any<CancellationToken>())
            .Returns((Result<ProcessLogoutResult>)logoutResult);

        // Act
        IActionResult result = await this._controller.AdminLogout(sessionId.Value, CancellationToken.None);

        // Assert
        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        LogoutResponse response = ok.Value.ShouldBeOfType<LogoutResponse>();
        response.Success.ShouldBeTrue();
        response.Message.ShouldBe("Session terminated");
    }
}
