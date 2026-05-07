
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenIdentityStack.Api.Authentication;
using OpenIdentityStack.Application.Federation.Commands;
using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Api.Tests.Authentication;
/// <summary>
/// Unit tests for ExternalCallbackController.
/// </summary>
public sealed class ExternalCallbackControllerTests
{
    private readonly IJitProvisionUserUseCase _jitProvisionUseCase;
    private readonly ILogger<ExternalCallbackController> _logger;
    private readonly ExternalCallbackController _controller;
    private readonly IAuthenticationService _authService;

    public ExternalCallbackControllerTests()
    {
        this._jitProvisionUseCase = Substitute.For<IJitProvisionUserUseCase>();
        this._logger = Substitute.For<ILogger<ExternalCallbackController>>();
        this._authService = Substitute.For<IAuthenticationService>();
        this._controller = new ExternalCallbackController(this._jitProvisionUseCase, this._logger);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = this.CreateServiceProvider()
        };
        this._controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private IServiceProvider CreateServiceProvider()
    {
        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IAuthenticationService)).Returns(this._authService);
        return serviceProvider;
    }

    [Fact]
    public async Task ExternalCallback_AuthenticationFails_RedirectsToLoginWithError()
    {
        // Arrange
        string provider = "google";
        this._authService
            .AuthenticateAsync(Arg.Any<HttpContext>(), provider)
            .Returns(AuthenticateResult.Fail("Auth failed"));

        // Act
        IActionResult result = await this._controller.ExternalCallback(provider, "/dashboard");

        // Assert
        RedirectResult redirectResult = result.ShouldBeOfType<RedirectResult>();
        redirectResult.Url.ShouldContain("/login?error=external_auth_failed");
        redirectResult.Url.ShouldContain("returnUrl=%2Fdashboard");
    }

    // Note: AuthenticationTicket requires a non-null principal, so we cannot test the 
    // "no principal" path directly. The controller guards against this scenario but 
    // AuthenticateResult.Success itself cannot be created with a null principal.

    [Fact]
    public async Task ExternalCallback_NoSubjectId_RedirectsToLoginWithError()
    {
        // Arrange
        string provider = "microsoft";
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Email, "user@example.com"),
                new Claim(ClaimTypes.Name, "Test User")
            ],
            "test"));

        var authResult = AuthenticateResult.Success(
            new AuthenticationTicket(principal, provider));
        this._authService
            .AuthenticateAsync(Arg.Any<HttpContext>(), provider)
            .Returns(authResult);

        // Act
        IActionResult result = await this._controller.ExternalCallback(provider);

        // Assert
        RedirectResult redirectResult = result.ShouldBeOfType<RedirectResult>();
        redirectResult.Url.ShouldContain("/login?error=no_subject");
    }

    [Fact]
    public async Task ExternalCallback_JitProvisioningFails_RedirectsToLoginWithError()
    {
        // Arrange
        string provider = "okta";
        string userId = "user-123";
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Email, "user@example.com")
            ],
            "test"));

        var authResult = AuthenticateResult.Success(
            new AuthenticationTicket(principal, provider));
        this._authService
            .AuthenticateAsync(Arg.Any<HttpContext>(), provider)
            .Returns(authResult);

        // Return a failure result using implicit conversion from DomainError
        Result<JitProvisionUserResult> failureResult = DomainError.Failure("Test.Error", "Provisioning failed");
        this._jitProvisionUseCase
            .ExecuteAsync(Arg.Any<JitProvisionUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(failureResult);

        // Act
        IActionResult result = await this._controller.ExternalCallback(provider);

        // Assert
        RedirectResult redirectResult = result.ShouldBeOfType<RedirectResult>();
        redirectResult.Url.ShouldContain("/login?error=provisioning_failed");
    }

    [Fact]
    public async Task ExternalCallback_SuccessfulAuthentication_RedirectsToReturnUrl()
    {
        // Arrange
        string provider = "google";
        string userId = Guid.NewGuid().ToString();
        string returnUrl = "/dashboard";
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Email, "user@example.com"),
                new Claim(ClaimTypes.Name, "Test User")
            ],
            "test"));

        var authResult = AuthenticateResult.Success(
            new AuthenticationTicket(principal, provider));
        this._authService
            .AuthenticateAsync(Arg.Any<HttpContext>(), provider)
            .Returns(authResult);

        // Return a success result using implicit conversion from JitProvisionUserResult
        Result<JitProvisionUserResult> successResult =
            new JitProvisionUserResult(UserId.Create(), false, "user@example.com", "Test User");
        this._jitProvisionUseCase
            .ExecuteAsync(Arg.Any<JitProvisionUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(successResult);

        // Act
        IActionResult result = await this._controller.ExternalCallback(provider, returnUrl);

        // Assert
        RedirectResult redirectResult = result.ShouldBeOfType<RedirectResult>();
        redirectResult.Url.ShouldBe(returnUrl);
    }

    [Fact]
    public async Task ExternalCallback_NoReturnUrl_RedirectsToRoot()
    {
        // Arrange
        string provider = "google";
        string userId = Guid.NewGuid().ToString();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId)
            ],
            "test"));

        var authResult = AuthenticateResult.Success(
            new AuthenticationTicket(principal, provider));
        this._authService
            .AuthenticateAsync(Arg.Any<HttpContext>(), provider)
            .Returns(authResult);

        Result<JitProvisionUserResult> successResult =
            new JitProvisionUserResult(UserId.Create(), true, null, "New User");
        this._jitProvisionUseCase
            .ExecuteAsync(Arg.Any<JitProvisionUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(successResult);

        // Act
        IActionResult result = await this._controller.ExternalCallback(provider);

        // Assert
        RedirectResult redirectResult = result.ShouldBeOfType<RedirectResult>();
        redirectResult.Url.ShouldBe("/");
    }

    [Fact]
    public async Task ExternalCallback_UsesSubClaimWhenNameIdentifierMissing()
    {
        // Arrange
        string provider = "okta";
        string subjectId = "sub-123";
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", subjectId),
                new Claim("email", "user@example.com")
            ],
            "test"));

        var authResult = AuthenticateResult.Success(
            new AuthenticationTicket(principal, provider));
        this._authService
            .AuthenticateAsync(Arg.Any<HttpContext>(), provider)
            .Returns(authResult);

        Result<JitProvisionUserResult> successResult =
            new JitProvisionUserResult(UserId.Create(), false, "user@example.com", "User");
        this._jitProvisionUseCase
            .ExecuteAsync(Arg.Any<JitProvisionUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(successResult);

        // Act
        IActionResult result = await this._controller.ExternalCallback(provider);

        // Assert
        result.ShouldBeOfType<RedirectResult>();
        await this._jitProvisionUseCase.Received(1).ExecuteAsync(
            Arg.Any<JitProvisionUserCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Challenge_ReturnsChallengeWithCorrectProperties()
    {
        // Arrange
        string provider = "google";
        string returnUrl = "/profile";

        // Act
        IActionResult result = this._controller.Challenge(provider, returnUrl);

        // Assert
        ChallengeResult challengeResult = result.ShouldBeOfType<ChallengeResult>();
        challengeResult.AuthenticationSchemes.ShouldContain(provider);
        challengeResult.Properties.ShouldNotBeNull();
        challengeResult.Properties!.RedirectUri!.ShouldContain($"/signin/{provider}");
        challengeResult.Properties.RedirectUri!.ShouldContain("returnUrl=%2Fprofile");
    }

    [Fact]
    public void Challenge_WithNoReturnUrl_UsesRootAsDefault()
    {
        // Arrange
        string provider = "microsoft";

        // Act
        IActionResult result = this._controller.Challenge(provider);

        // Assert
        ChallengeResult challengeResult = result.ShouldBeOfType<ChallengeResult>();
        challengeResult.Properties!.RedirectUri!.ShouldContain("returnUrl=%2F");
    }

    [Fact]
    public void Challenge_SetsLoginProviderInItems()
    {
        // Arrange
        string provider = "okta";

        // Act
        IActionResult result = this._controller.Challenge(provider);

        // Assert
        ChallengeResult challengeResult = result.ShouldBeOfType<ChallengeResult>();
        challengeResult.Properties!.Items["LoginProvider"].ShouldBe(provider);
    }
}
