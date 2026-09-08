
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using OpenIddict.Abstractions;
using OpenIdentityStack.Api.Authentication;
using OpenIdentityStack.Api.Tests.Helpers;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Domain.Common;
using System.Security.Claims;

using SharedKernel;
namespace OpenIdentityStack.Api.Tests.Authentication;
public sealed class LogoutControllerTests : IDisposable
{
    private readonly IProcessLogoutUseCase _processLogoutUseCase;
    private readonly IFrontChannelLogoutService _frontChannelLogoutService;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogoutNotifier _logoutNotifier;
    private readonly IOpenIddictRequestService _requestService;
    private readonly IAntiforgery _antiforgery;
    private readonly LogoutController _controller;

    public LogoutControllerTests()
    {
        this._processLogoutUseCase = Substitute.For<IProcessLogoutUseCase>();
        this._frontChannelLogoutService = Substitute.For<IFrontChannelLogoutService>();
        this._sessionRepository = Substitute.For<ISessionRepository>();
        this._logoutNotifier = Substitute.For<ILogoutNotifier>();
        this._requestService = Substitute.For<IOpenIddictRequestService>();
        this._antiforgery = Substitute.For<IAntiforgery>();
        this._antiforgery.ValidateRequestAsync(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);

        this._controller = new LogoutController(
            this._processLogoutUseCase,
            this._frontChannelLogoutService,
            this._sessionRepository,
            this._logoutNotifier,
            this._requestService,
            this._antiforgery);

        DefaultHttpContext httpContext = HttpContextTestHelper.CreateWithAuthenticationServices();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["confirm_logout"] = "true"
        });
        this._controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        this._controller.TempData = new TempDataDictionary(
            this._controller.HttpContext,
            Substitute.For<ITempDataProvider>());
    }

    [Fact]
    public async Task Logout_ShouldRenderConfirmation_ForProtocolPostWithoutConfirmationMarker()
    {
        // Arrange
        this._controller.HttpContext.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["client_id"] = "client"
        });
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(new OpenIddictRequest
        {
            ClientId = "client",
            PostLogoutRedirectUri = "https://example.com/logout",
            State = "protocol-state"
        });

        // Act
        IActionResult result = await this._controller.Logout();

        // Assert
        ViewResult view = result.ShouldBeOfType<ViewResult>();
        view.ViewName.ShouldBe("~/Authentication/Views/LogoutConfirmation.cshtml");
        view.ViewData["ClientId"].ShouldBe("client");
        view.ViewData["PostLogoutRedirectUri"].ShouldBe("https://example.com/logout");
        view.ViewData["State"].ShouldBe("protocol-state");
        await this._antiforgery.DidNotReceive().ValidateRequestAsync(Arg.Any<HttpContext>());
        await this._processLogoutUseCase.DidNotReceive().ExecuteAsync(
            Arg.Any<SessionId>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
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

    public void Dispose() => this._controller.Dispose();

    [Fact]
    public async Task Logout_Get_RendersConfirmationWithoutTerminatingSession()
    {
        // Arrange
        this._controller.HttpContext.Request.Method = HttpMethods.Get;
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(new OpenIddictRequest());

        // Act
        IActionResult result = await this._controller.Logout();

        // Assert
        ViewResult view = result.ShouldBeOfType<ViewResult>();
        view.ViewName.ShouldBe("~/Authentication/Views/LogoutConfirmation.cshtml");
        await this._processLogoutUseCase.DidNotReceive().ExecuteAsync(
            Arg.Any<SessionId>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_WithFrontChannelIframes_RendersLogoutViewWithScopedFramePolicy()
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

        this._processLogoutUseCase.ExecuteAsync(Arg.Any<SessionId>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Result<ProcessLogoutResult>)result);

        this._controller.HttpContext.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; frame-src 'none'; object-src 'none';";

        // Act
        IActionResult response = await this._controller.Logout();

        // Assert
        ViewResult view = response.ShouldBeOfType<ViewResult>();
        view.ViewName.ShouldBe("~/Authentication/Views/Logout.cshtml");
        this._controller.HttpContext.Response.Headers["Content-Security-Policy"].ToString()
            .ShouldContain("frame-src 'self' https://client");
        this._controller.HttpContext.Response.Headers["Content-Security-Policy"].ToString()
            .ShouldContain("object-src 'none'");
        this._controller.HttpContext.Response.Headers["Content-Security-Policy"].ToString()
            .ShouldNotContain("frame-src 'none'");
        
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

        this._processLogoutUseCase.ExecuteAsync(Arg.Any<SessionId>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
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

        this._processLogoutUseCase.ExecuteAsync(Arg.Any<SessionId>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
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
        this._processLogoutUseCase.ExecuteAsync(Arg.Any<SessionId>(), Arg.Is<string?>(value => value == null), Arg.Any<string>(), Arg.Any<CancellationToken>())
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

        this._processLogoutUseCase.ExecuteAsync(Arg.Any<SessionId>(), Arg.Is<string?>(value => value == null), Arg.Any<string>(), Arg.Any<CancellationToken>())
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
