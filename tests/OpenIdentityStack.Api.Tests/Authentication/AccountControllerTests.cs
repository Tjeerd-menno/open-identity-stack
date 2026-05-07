using System.Globalization;
using System.Net;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using OpenIdentityStack.Api.Authentication;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Federation.Commands;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Settings;

using SharedKernel;
namespace OpenIdentityStack.Api.Tests.Authentication;

public class AccountControllerTests : IDisposable
{
    private readonly IValidateUserCredentialsUseCase _validateCredentialsUseCase;
    private readonly ICreateSessionUseCase _createSessionUseCase;
    private readonly IAuthenticationSettingsRepository _authSettingsRepository;
    private readonly IUpstreamProviderRepository _providerRepository;
    private readonly IPermissionChecker _permissionChecker;
    private readonly IUserRepository _userRepository;
    private readonly IDynamicAuthenticationSchemeService _schemeService;
    private readonly IJitProvisionUserUseCase _jitProvisionUseCase;
    private readonly AccountController _controller;
    private readonly IAuthenticationService _authService;

    public AccountControllerTests()
    {
        this._validateCredentialsUseCase = Substitute.For<IValidateUserCredentialsUseCase>();
        this._createSessionUseCase = Substitute.For<ICreateSessionUseCase>();
        this._authService = Substitute.For<IAuthenticationService>();
        this._authSettingsRepository = Substitute.For<IAuthenticationSettingsRepository>();
        this._providerRepository = Substitute.For<IUpstreamProviderRepository>();
        this._permissionChecker = Substitute.For<IPermissionChecker>();
        this._userRepository = Substitute.For<IUserRepository>();
        this._schemeService = Substitute.For<IDynamicAuthenticationSchemeService>();
        this._jitProvisionUseCase = Substitute.For<IJitProvisionUserUseCase>();

        // Setup default auth settings - local is default
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
        var defaultSettings = AuthenticationSettings.CreateDefault(dateTimeProvider);
        this._authSettingsRepository.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(defaultSettings));

        this._controller = new AccountController(
            this._validateCredentialsUseCase,
            this._createSessionUseCase,
            this._authSettingsRepository,
            this._providerRepository,
            this._permissionChecker,
            this._userRepository,
            this._schemeService,
            this._jitProvisionUseCase);

        this.SetupHttpContext();
    }

    public void Dispose()
    {
        this._controller.Dispose();
        GC.SuppressFinalize(this);
    }

    private void SetupHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        httpContext.Request.Headers.UserAgent = "TestBrowser/1.0";

        // Setup authentication service
        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IAuthenticationService)).Returns(this._authService);
        httpContext.RequestServices = serviceProvider;

        // Setup TempData for View
        ITempDataProvider tempDataProvider = Substitute.For<ITempDataProvider>();
        this._controller.TempData = new TempDataDictionary(httpContext, tempDataProvider);

        this._controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Setup Url helper for IsLocalUrl check
        IUrlHelper urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.IsLocalUrl(Arg.Any<string>()).Returns(x =>
        {
            string url = x.Arg<string>();
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            return url.StartsWith('/')
                && !url.StartsWith("//", StringComparison.Ordinal)
                && !url.StartsWith("/\\", StringComparison.Ordinal);
        });
        this._controller.Url = urlHelper;
    }

    #region Login GET Tests

    [Fact]
    public async Task Login_Get_ReturnsViewResult()
    {
        // Act
        IActionResult result = await this._controller.Login();

        // Assert
        ViewResult viewResult = Assert.IsType<ViewResult>(result);
        Assert.Null(viewResult.ViewData["ReturnUrl"]);
    }

    [Fact]
    public async Task Login_Get_WithReturnUrl_SetsReturnUrlInViewData()
    {
        // Arrange
        string returnUrl = "/dashboard";

        // Act
        IActionResult result = await this._controller.Login(returnUrl);

        // Assert
        ViewResult viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal(returnUrl, viewResult.ViewData["ReturnUrl"]);
    }

    [Fact]
    public async Task Login_Get_WithNullReturnUrl_ReturnsViewWithNullReturnUrl()
    {
        // Act
        IActionResult result = await this._controller.Login(returnUrl: null, fresh: false);

        // Assert
        ViewResult viewResult = Assert.IsType<ViewResult>(result);
        Assert.Null(viewResult.ViewData["ReturnUrl"]);
    }

    #endregion

    #region Login POST Tests - ModelState Validation

    [Fact]
    public async Task Login_Post_WithInvalidModelState_ReturnsViewWithModel()
    {
        // Arrange
        var model = new LoginViewModel { Email = "", Password = "" };
        this._controller.ModelState.AddModelError("Email", "Email is required");

        // Act
        IActionResult result = await this._controller.Login(model);

        // Assert
        ViewResult viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(model, viewResult.Model);
    }

    [Fact]
    public async Task Login_Post_WithInvalidModelState_PreservesReturnUrl()
    {
        // Arrange
        var model = new LoginViewModel { Email = "", Password = "" };
        string returnUrl = "/profile";
        this._controller.ModelState.AddModelError("Email", "Email is required");

        // Act
        IActionResult result = await this._controller.Login(model, returnUrl);

        // Assert
        ViewResult viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal(returnUrl, viewResult.ViewData["ReturnUrl"]);
    }

    #endregion

    #region Login POST Tests - Invalid Credentials

    [Fact]
    public async Task Login_Post_WithInvalidCredentials_ReturnsViewWithError()
    {
        // Arrange
        var model = new LoginViewModel { Email = "user@example.com", Password = "wrong" };
        // DomainError.Unauthorized prepends "Unauthorized." to the code
        this._validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateUserCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(DomainError.Unauthorized("User.InvalidCredentials", "Invalid credentials"));

        // Act
        IActionResult result = await this._controller.Login(model);

        // Assert
        ViewResult viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(model, viewResult.Model);
        Assert.True(this._controller.ModelState.ContainsKey(string.Empty));
        ModelErrorCollection errors = this._controller.ModelState[string.Empty]!.Errors;
        Assert.Single(errors);
        Assert.Equal("Invalid email or password.", errors[0].ErrorMessage);
    }

    [Fact]
    public async Task Login_Post_WithDisabledAccount_ReturnsViewWithDisabledError()
    {
        // Arrange
        var model = new LoginViewModel { Email = "disabled@example.com", Password = "pass" };
        // DomainError.Forbidden prepends "Forbidden." to the code
        this._validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateUserCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(DomainError.Forbidden("User.AccountDisabled", "Account is disabled"));

        // Act
        IActionResult result = await this._controller.Login(model);

        // Assert
        ViewResult viewResult = Assert.IsType<ViewResult>(result);
        ModelErrorCollection errors = this._controller.ModelState[string.Empty]!.Errors;
        Assert.Contains("disabled", errors[0].ErrorMessage.ToLower(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Login_Post_WithUnverifiedAccount_ReturnsViewWithVerifyError()
    {
        // Arrange
        var model = new LoginViewModel { Email = "unverified@example.com", Password = "pass" };
        // DomainError.Forbidden prepends "Forbidden." to the code
        this._validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateUserCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(DomainError.Forbidden("User.AccountNotVerified", "Account not verified"));

        // Act
        IActionResult result = await this._controller.Login(model);

        // Assert
        ViewResult viewResult = Assert.IsType<ViewResult>(result);
        ModelErrorCollection errors = this._controller.ModelState[string.Empty]!.Errors;
        Assert.Contains("verify", errors[0].ErrorMessage.ToLower(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Login_Post_WithUnknownError_ReturnsViewWithGenericError()
    {
        // Arrange
        var model = new LoginViewModel { Email = "user@example.com", Password = "pass" };
        this._validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateUserCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(DomainError.Failure("Unknown.Error", "Something went wrong"));

        // Act
        IActionResult result = await this._controller.Login(model);

        // Assert
        ViewResult viewResult = Assert.IsType<ViewResult>(result);
        ModelErrorCollection errors = this._controller.ModelState[string.Empty]!.Errors;
        Assert.Contains("error occurred", errors[0].ErrorMessage.ToLower(CultureInfo.InvariantCulture));
    }

    #endregion

    #region Login POST Tests - Successful Login

    [Fact]
    public async Task Login_Post_WithValidCredentials_CallsSignInAsync()
    {
        // Arrange
        var model = new LoginViewModel { Email = "user@example.com", Password = "correct", RememberMe = false };
        var userId = UserId.Create();
        var sessionId = SessionId.Create();

        this._validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateUserCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateUserCredentialsResult(userId, "user@example.com", "Test User"));

        this._createSessionUseCase.ExecuteAsync(Arg.Any<CreateSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateSessionResult(sessionId));

        // Act
        await this._controller.Login(model);

        // Assert
        await this._authService.Received(1).SignInAsync(
            Arg.Any<HttpContext>(),
            "Cookies",
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<AuthenticationProperties>());
    }

    [Fact]
    public async Task Login_Post_WithValidCredentials_RedirectsToHome()
    {
        // Arrange
        var model = new LoginViewModel { Email = "user@example.com", Password = "correct" };
        var userId = UserId.Create();
        var sessionId = SessionId.Create();

        this._validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateUserCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateUserCredentialsResult(userId, "user@example.com", "Test User"));

        this._createSessionUseCase.ExecuteAsync(Arg.Any<CreateSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateSessionResult(sessionId));

        // Act
        IActionResult result = await this._controller.Login(model);

        // Assert
        RedirectResult redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/", redirectResult.Url);
    }

    [Fact]
    public async Task Login_Post_WithValidCredentialsAndReturnUrl_RedirectsToReturnUrl()
    {
        // Arrange
        var model = new LoginViewModel { Email = "user@example.com", Password = "correct" };
        string returnUrl = "/dashboard";
        var userId = UserId.Create();
        var sessionId = SessionId.Create();

        this._validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateUserCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateUserCredentialsResult(userId, "user@example.com", "Test User"));

        this._createSessionUseCase.ExecuteAsync(Arg.Any<CreateSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateSessionResult(sessionId));

        // Act
        IActionResult result = await this._controller.Login(model, returnUrl);

        // Assert
        LocalRedirectResult redirectResult = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal(returnUrl, redirectResult.Url);
    }

    [Fact]
    public async Task Login_Post_WithNonLocalReturnUrl_RedirectsToHome()
    {
        // Arrange
        var model = new LoginViewModel { Email = "user@example.com", Password = "correct" };
        string returnUrl = "https://evil.com/phishing";
        var userId = UserId.Create();
        var sessionId = SessionId.Create();

        this._validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateUserCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateUserCredentialsResult(userId, "user@example.com", "Test User"));

        this._createSessionUseCase.ExecuteAsync(Arg.Any<CreateSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateSessionResult(sessionId));

        // Act
        IActionResult result = await this._controller.Login(model, returnUrl);

        // Assert
        RedirectResult redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/", redirectResult.Url);
    }

    [Fact]
    public async Task Login_Post_WithRememberMe_SetsIsPersistentTrue()
    {
        // Arrange
        var model = new LoginViewModel { Email = "user@example.com", Password = "correct", RememberMe = true };
        var userId = UserId.Create();
        var sessionId = SessionId.Create();
        AuthenticationProperties? capturedProperties = null;

        this._validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateUserCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateUserCredentialsResult(userId, "user@example.com", "Test User"));

        this._createSessionUseCase.ExecuteAsync(Arg.Any<CreateSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateSessionResult(sessionId));

        this._authService.SignInAsync(Arg.Any<HttpContext>(), Arg.Any<string>(), Arg.Any<ClaimsPrincipal>(), Arg.Do<AuthenticationProperties>(p => capturedProperties = p))
            .Returns(Task.CompletedTask);

        // Act
        await this._controller.Login(model);

        // Assert
        Assert.NotNull(capturedProperties);
        Assert.True(capturedProperties.IsPersistent);
    }

    [Fact]
    public async Task Login_Post_WithoutRememberMe_SetsIsPersistentFalse()
    {
        // Arrange
        var model = new LoginViewModel { Email = "user@example.com", Password = "correct", RememberMe = false };
        var userId = UserId.Create();
        var sessionId = SessionId.Create();
        AuthenticationProperties? capturedProperties = null;

        this._validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateUserCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateUserCredentialsResult(userId, "user@example.com", "Test User"));

        this._createSessionUseCase.ExecuteAsync(Arg.Any<CreateSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateSessionResult(sessionId));

        this._authService.SignInAsync(Arg.Any<HttpContext>(), Arg.Any<string>(), Arg.Any<ClaimsPrincipal>(), Arg.Do<AuthenticationProperties>(p => capturedProperties = p))
            .Returns(Task.CompletedTask);

        // Act
        await this._controller.Login(model);

        // Assert
        Assert.NotNull(capturedProperties);
        Assert.False(capturedProperties.IsPersistent);
    }

    [Fact]
    public async Task Login_Post_CreatesSession_WithCorrectIpAndUserAgent()
    {
        // Arrange
        var model = new LoginViewModel { Email = "user@example.com", Password = "correct" };
        var userId = UserId.Create();
        var sessionId = SessionId.Create();
        CreateSessionCommand? capturedCommand = null;

        this._validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateUserCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateUserCredentialsResult(userId, "user@example.com", "Test User"));

        this._createSessionUseCase.ExecuteAsync(Arg.Do<CreateSessionCommand>(c => capturedCommand = c), Arg.Any<CancellationToken>())
            .Returns(new CreateSessionResult(sessionId));

        // Act
        await this._controller.Login(model);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(userId, capturedCommand.UserId);
        Assert.Equal("127.0.0.1", capturedCommand.IpAddress);
        Assert.Equal("TestBrowser/1.0", capturedCommand.UserAgent);
    }

    [Fact]
    public async Task Login_Post_WithValidCredentials_IncludesSessionIdInClaims()
    {
        // Arrange
        var model = new LoginViewModel { Email = "user@example.com", Password = "correct" };
        var userId = UserId.Create();
        var sessionId = SessionId.Create();
        ClaimsPrincipal? capturedPrincipal = null;

        this._validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateUserCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateUserCredentialsResult(userId, "user@example.com", "Test User"));

        this._createSessionUseCase.ExecuteAsync(Arg.Any<CreateSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateSessionResult(sessionId));

        this._authService.SignInAsync(Arg.Any<HttpContext>(), Arg.Any<string>(), Arg.Do<ClaimsPrincipal>(p => capturedPrincipal = p), Arg.Any<AuthenticationProperties>())
            .Returns(Task.CompletedTask);

        // Act
        await this._controller.Login(model);

        // Assert
        Assert.NotNull(capturedPrincipal);
        Claim? sessionClaim = capturedPrincipal.FindFirst("session_id");
        Assert.NotNull(sessionClaim);
        Assert.Equal(sessionId.Value.ToString(), sessionClaim.Value);
    }

    [Fact]
    public async Task Login_Post_WhenSessionCreationFails_StillSignsIn()
    {
        // Arrange
        var model = new LoginViewModel { Email = "user@example.com", Password = "correct" };
        var userId = UserId.Create();

        this._validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateUserCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateUserCredentialsResult(userId, "user@example.com", "Test User"));

        this._createSessionUseCase.ExecuteAsync(Arg.Any<CreateSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(DomainError.Failure("Session.CreateFailed", "Failed to create session"));

        // Act
        IActionResult result = await this._controller.Login(model);

        // Assert
        // Should still redirect to home (login succeeded, session creation is optional)
        RedirectResult redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/", redirectResult.Url);

        await this._authService.Received(1).SignInAsync(
            Arg.Any<HttpContext>(),
            "Cookies",
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<AuthenticationProperties>());
    }

    [Fact]
    public async Task Login_Post_WhenSessionCreationFails_DoesNotIncludeSessionIdClaim()
    {
        // Arrange
        var model = new LoginViewModel { Email = "user@example.com", Password = "correct" };
        var userId = UserId.Create();
        ClaimsPrincipal? capturedPrincipal = null;

        this._validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateUserCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateUserCredentialsResult(userId, "user@example.com", "Test User"));

        this._createSessionUseCase.ExecuteAsync(Arg.Any<CreateSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(DomainError.Failure("Session.CreateFailed", "Failed to create session"));

        this._authService.SignInAsync(Arg.Any<HttpContext>(), Arg.Any<string>(), Arg.Do<ClaimsPrincipal>(p => capturedPrincipal = p), Arg.Any<AuthenticationProperties>())
            .Returns(Task.CompletedTask);

        // Act
        await this._controller.Login(model);

        // Assert
        Assert.NotNull(capturedPrincipal);
        Claim? sessionClaim = capturedPrincipal.FindFirst("session_id");
        Assert.Null(sessionClaim);
    }

    [Fact]
    public async Task Login_Post_IncludesUserClaimsInPrincipal()
    {
        // Arrange
        var model = new LoginViewModel { Email = "user@example.com", Password = "correct" };
        var userId = UserId.Create();
        var sessionId = SessionId.Create();
        ClaimsPrincipal? capturedPrincipal = null;

        this._validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateUserCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateUserCredentialsResult(userId, "user@example.com", "Test User"));

        this._createSessionUseCase.ExecuteAsync(Arg.Any<CreateSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateSessionResult(sessionId));

        this._authService.SignInAsync(Arg.Any<HttpContext>(), Arg.Any<string>(), Arg.Do<ClaimsPrincipal>(p => capturedPrincipal = p), Arg.Any<AuthenticationProperties>())
            .Returns(Task.CompletedTask);

        // Act
        await this._controller.Login(model);

        // Assert
        Assert.NotNull(capturedPrincipal);
        Assert.Equal(userId.Value.ToString(), capturedPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("user@example.com", capturedPrincipal.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal("Test User", capturedPrincipal.FindFirst(ClaimTypes.Name)?.Value);
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task Logout_CallsSignOutAsync()
    {
        // Act
        await this._controller.Logout();

        // Assert
        await this._authService.Received(1).SignOutAsync(
            Arg.Any<HttpContext>(),
            "Cookies",
            Arg.Any<AuthenticationProperties>());
    }

    [Fact]
    public async Task Logout_RedirectsToHome()
    {
        // Act
        IActionResult result = await this._controller.Logout();

        // Assert
        RedirectResult redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/", redirectResult.Url);
    }

    #endregion

    #region AccessDenied Tests

    [Fact]
    public void AccessDenied_ReturnsViewResult()
    {
        // Act
        IActionResult result = this._controller.AccessDenied();

        // Assert
        Assert.IsType<ViewResult>(result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Login_Post_WithEmptyReturnUrl_RedirectsToHome()
    {
        // Arrange
        var model = new LoginViewModel { Email = "user@example.com", Password = "correct" };
        var userId = UserId.Create();
        var sessionId = SessionId.Create();

        this._validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateUserCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateUserCredentialsResult(userId, "user@example.com", "Test User"));

        this._createSessionUseCase.ExecuteAsync(Arg.Any<CreateSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateSessionResult(sessionId));

        // Act
        IActionResult result = await this._controller.Login(model, string.Empty);

        // Assert
        RedirectResult redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/", redirectResult.Url);
    }

    [Fact]
    public async Task Login_Post_WithProtocolRelativeUrl_RedirectsToHome()
    {
        // Arrange - protocol-relative URL is a security risk
        var model = new LoginViewModel { Email = "user@example.com", Password = "correct" };
        string returnUrl = "//evil.com/phishing";
        var userId = UserId.Create();
        var sessionId = SessionId.Create();

        this._validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateUserCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateUserCredentialsResult(userId, "user@example.com", "Test User"));

        this._createSessionUseCase.ExecuteAsync(Arg.Any<CreateSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateSessionResult(sessionId));

        // Act
        IActionResult result = await this._controller.Login(model, returnUrl);

        // Assert
        RedirectResult redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/", redirectResult.Url);
    }

    [Fact]
    public async Task Login_Post_WithNullIpAddress_UsesFallbackIp()
    {
        // Arrange - reset context with null IP
        var httpContext = new DefaultHttpContext();
        // RemoteIpAddress is null by default in DefaultHttpContext
        httpContext.Request.Headers.UserAgent = "TestBrowser/1.0";

        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IAuthenticationService)).Returns(this._authService);
        httpContext.RequestServices = serviceProvider;

        ITempDataProvider tempDataProvider = Substitute.For<ITempDataProvider>();
        this._controller.TempData = new TempDataDictionary(httpContext, tempDataProvider);
        this._controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        IUrlHelper urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.IsLocalUrl(Arg.Any<string>()).Returns(false);
        this._controller.Url = urlHelper;

        var model = new LoginViewModel { Email = "user@example.com", Password = "correct" };
        var userId = UserId.Create();
        var sessionId = SessionId.Create();
        CreateSessionCommand? capturedCommand = null;

        this._validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateUserCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateUserCredentialsResult(userId, "user@example.com", "Test User"));

        this._createSessionUseCase.ExecuteAsync(Arg.Do<CreateSessionCommand>(c => capturedCommand = c), Arg.Any<CancellationToken>())
            .Returns(new CreateSessionResult(sessionId));

        // Act
        await this._controller.Login(model);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal("0.0.0.0", capturedCommand.IpAddress);
    }

    #endregion
}
