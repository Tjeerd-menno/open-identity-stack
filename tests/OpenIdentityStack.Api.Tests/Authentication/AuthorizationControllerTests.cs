using System.Collections.Immutable;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using OpenIdentityStack.Api.Authentication;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Application.Sessions.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Users;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

using SharedKernel;
namespace OpenIdentityStack.Api.Tests.Authentication;

public class AuthorizationControllerTests
{
    private const string AcrClaim = "acr";
    private const string SupportedAcrValue = "1";
    private const string RequestedUserInfoClaim = "requested_userinfo_claim";
    private const string UpdatedAtClaim = "updated_at";

    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly IUserRepository _userRepository;
    private readonly IGetUserEffectiveRolesQueryHandler _getUserEffectiveRolesQueryHandler;
    private readonly IGetGroupClaimsForUserQueryHandler _getGroupClaimsForUserQueryHandler;
    private readonly IAddClientSessionUseCase _addClientSessionUseCase;
    private readonly IValidateSessionQueryHandler _validateSessionQueryHandler;
    private readonly IOpenIddictRequestService _requestService;
    private readonly IApplicationPermissionRegistryRepository _applicationPermissionRegistryRepository;
    private readonly AuthorizationController _controller;

    public AuthorizationControllerTests()
    {
        this._applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        this._scopeManager = Substitute.For<IOpenIddictScopeManager>();
        this._userRepository = Substitute.For<IUserRepository>();
        this._getUserEffectiveRolesQueryHandler = Substitute.For<IGetUserEffectiveRolesQueryHandler>();
        this._getGroupClaimsForUserQueryHandler = Substitute.For<IGetGroupClaimsForUserQueryHandler>();
        this._addClientSessionUseCase = Substitute.For<IAddClientSessionUseCase>();
        this._validateSessionQueryHandler = Substitute.For<IValidateSessionQueryHandler>();
        this._requestService = Substitute.For<IOpenIddictRequestService>();
        this._applicationPermissionRegistryRepository = Substitute.For<IApplicationPermissionRegistryRepository>();

        this._controller = new AuthorizationController(
            this._applicationManager,
            this._scopeManager,
            this._userRepository,
            this._getUserEffectiveRolesQueryHandler,
            this._getGroupClaimsForUserQueryHandler,
            this._addClientSessionUseCase,
            this._validateSessionQueryHandler,
            this._requestService,
            this._applicationPermissionRegistryRepository);

        var httpContext = new DefaultHttpContext();
        this._controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        this.SetupMockServices(null, authSuccess: false);
    }

    private void SetupMockServices(
        ClaimsPrincipal? principal = null,
        bool authSuccess = true,
        AuthenticationProperties? properties = null)
    {
        IAuthenticationService authService = Substitute.For<IAuthenticationService>();
        AuthenticationTicket? ticket = principal != null
            ? new AuthenticationTicket(
                principal,
                properties ?? new AuthenticationProperties(),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
            : null;

        authService.AuthenticateAsync(Arg.Any<HttpContext>(), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
            .Returns(authSuccess && ticket != null
                ? AuthenticateResult.Success(ticket)
                : AuthenticateResult.Fail("Auth failed"));
        authService.AuthenticateAsync(Arg.Any<HttpContext>(), "Cookies")
            .Returns(authSuccess && ticket != null
                ? AuthenticateResult.Success(new AuthenticationTicket(
                    principal!,
                    properties ?? new AuthenticationProperties(),
                    "Cookies"))
                : AuthenticateResult.Fail("Auth failed"));

        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IAuthenticationService)).Returns(authService);
        this._controller.HttpContext.RequestServices = serviceProvider;
    }

    private static bool HasTokenDestinations(Claim claim) =>
        claim.Properties.TryGetValue("destinations", out string? destinations)
        && !string.IsNullOrWhiteSpace(destinations);

    #region Authorize Tests

    [Fact]
    public async Task Authorize_WhenRequestIsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns((OpenIddictRequest?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => this._controller.Authorize());
    }

    [Fact]
    public async Task Authorize_WhenUserNotAuthenticated_RedirectsToLogin()
    {
        // Arrange
        var request = new OpenIddictRequest { ClientId = "test-client" };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        // User not authenticated (default)
        this._controller.HttpContext.Request.Path = "/connect/authorize";
        this._controller.HttpContext.Request.QueryString = new QueryString("?client_id=test-client");

        // Act
        IActionResult result = await this._controller.Authorize();

        // Assert
        RedirectResult redirect = Assert.IsType<RedirectResult>(result);
        Assert.Contains("/Account/Login", redirect.Url);
        Assert.Contains("returnUrl=", redirect.Url);
    }

    [Fact]
    public async Task Authorize_WithPromptNoneAndNoAuthenticatedUser_ReturnsLoginRequired()
    {
        // Arrange
        var request = new OpenIddictRequest
        {
            ClientId = "test-client",
            Prompt = "none"
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        // Act
        IActionResult result = await this._controller.Authorize();

        // Assert
        ForbidResult forbid = Assert.IsType<ForbidResult>(result);
        Assert.Equal(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Assert.Single(forbid.AuthenticationSchemes));
        Assert.Equal(
            OpenIddictConstants.Errors.LoginRequired,
            forbid.Properties!.Items[OpenIddictServerAspNetCoreConstants.Properties.Error]);
    }

    [Fact]
    public async Task Authorize_WhenPromptLoginRequested_ConsumesFreshLoginParametersInReturnUrl()
    {
        // Arrange
        var request = new OpenIddictRequest
        {
            ClientId = "test-client",
            Prompt = "login",
            MaxAge = 0
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) },
            "Cookies"));
        this._controller.ControllerContext.HttpContext.User = principal;
        this.SetupMockServices(principal);
        this._controller.HttpContext.Request.Path = "/connect/authorize";
        this._controller.HttpContext.Request.QueryString = new QueryString(
            "?client_id=test-client&prompt=login&max_age=0&scope=openid&state=abc");

        // Act
        IActionResult result = await this._controller.Authorize();

        // Assert
        RedirectResult redirect = Assert.IsType<RedirectResult>(result);
        Dictionary<string, Microsoft.Extensions.Primitives.StringValues> redirectQuery =
            QueryHelpers.ParseQuery(new Uri("https://example.test" + redirect.Url).Query);
        string returnUrl = redirectQuery["returnUrl"].Single()!;
        Assert.Contains("client_id=test-client", returnUrl);
        Assert.Contains("scope=openid", returnUrl);
        Assert.DoesNotContain("prompt=login", returnUrl);
        Assert.DoesNotContain("max_age=0", returnUrl);
        Assert.Contains("fresh=true", redirect.Url);
    }

    [Fact]
    public async Task Authorize_WithExpiredMaxAgeAndPromptNone_ReturnsLoginRequired()
    {
        // Arrange
        var request = new OpenIddictRequest
        {
            ClientId = "test-client",
            Prompt = "none",
            MaxAge = 1
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        long expiredAuthTime = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(OpenIddictConstants.Claims.AuthenticationTime, expiredAuthTime.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
            },
            "Cookies"));
        this._controller.ControllerContext.HttpContext.User = principal;

        // Act
        IActionResult result = await this._controller.Authorize();

        // Assert
        ForbidResult forbid = Assert.IsType<ForbidResult>(result);
        Assert.Equal(
            OpenIddictConstants.Errors.LoginRequired,
            forbid.Properties!.Items[OpenIddictServerAspNetCoreConstants.Properties.Error]);
    }

    [Fact]
    public async Task Authorize_WhenUserAuthenticated_ReturnsSignIn()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = SessionId.Create();
        var request = new OpenIddictRequest { ClientId = "test-client" };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        Claim[] claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim("session_id", sessionId.Value.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        this._controller.ControllerContext.HttpContext.User = principal;

        // Setup role and group claims handlers to return empty results
        this._getUserEffectiveRolesQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)Array.Empty<RoleDto>());
        this._getGroupClaimsForUserQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<GroupClaimDto>>)Array.Empty<GroupClaimDto>());

        // Setup scope manager to return empty resources
        this._scopeManager.ListResourcesAsync(Arg.Any<ImmutableArray<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        // Act
        IActionResult result = await this._controller.Authorize();

        // Assert
        Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
    }

    [Fact]
    public async Task Authorize_WhenSubjectClaimMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new OpenIddictRequest { ClientId = "test-client" };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        Claim[] claims = new[] { new Claim(ClaimTypes.Name, "Test User") };
        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        this._controller.ControllerContext.HttpContext.User = principal;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => this._controller.Authorize());
    }

    [Fact]
    public async Task Authorize_AddsClientSession_WhenClientIdPresent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = SessionId.Create();
        var request = new OpenIddictRequest { ClientId = "test-client" };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        Claim[] claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("session_id", sessionId.Value.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        this._controller.ControllerContext.HttpContext.User = principal;

        this._getUserEffectiveRolesQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)Array.Empty<RoleDto>());
        this._getGroupClaimsForUserQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<GroupClaimDto>>)Array.Empty<GroupClaimDto>());
        this._scopeManager.ListResourcesAsync(Arg.Any<ImmutableArray<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        // Act
        await this._controller.Authorize();

        // Assert
        await this._addClientSessionUseCase.Received(1).ExecuteAsync(
            Arg.Is<AddClientSessionCommand>(c => c.SessionId == sessionId && c.ClientId == "test-client"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_DoesNotExposeLegacySessionClaimInTokenDestinations()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = SessionId.Create();
        var request = new OpenIddictRequest
        {
            ClientId = "test-client",
            Scope = OpenIddictConstants.Scopes.OpenId
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        Claim[] claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("session_id", sessionId.Value.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        this._controller.ControllerContext.HttpContext.User = principal;

        this._getUserEffectiveRolesQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)Array.Empty<RoleDto>());
        this._getGroupClaimsForUserQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<GroupClaimDto>>)Array.Empty<GroupClaimDto>());
        this._scopeManager.ListResourcesAsync(Arg.Any<ImmutableArray<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        // Act
        IActionResult result = await this._controller.Authorize();

        // Assert
        SignInResult signIn = Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
        Claim legacySessionClaim = signIn.Principal!.Claims.Single(c => c.Type == "session_id");
        Assert.False(HasTokenDestinations(legacySessionClaim));
    }

    [Fact]
    public async Task Authorize_AddsRoleClaims_WhenUserHasRoles()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new OpenIddictRequest { ClientId = "test-client" };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        Claim[] claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        this._controller.ControllerContext.HttpContext.User = principal;

#pragma warning disable CA1861
        var roleDto = new RoleDto(RoleId.Create().Value, "Admin", "Admin", "Administrator role", true, true, (IReadOnlyList<string>)new[] { "read", "write" });
        this._getUserEffectiveRolesQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)new[] { roleDto });
#pragma warning restore CA1861
        this._getGroupClaimsForUserQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<GroupClaimDto>>)Array.Empty<GroupClaimDto>());
        this._scopeManager.ListResourcesAsync(Arg.Any<ImmutableArray<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        // Act
        IActionResult result = await this._controller.Authorize();

        // Assert
        SignInResult signIn = Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
        Assert.Contains(signIn.Principal!.Claims, c => c.Type == OpenIddictConstants.Claims.Role && c.Value == "Admin");
    }

    [Fact]
    public async Task Authorize_ExpandsWildcardRolePermissionsToConcretePermissionClaims()
    {
        var userId = Guid.NewGuid();
        var request = new OpenIddictRequest { ClientId = "test-client" };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            "Cookies"));
        this._controller.ControllerContext.HttpContext.User = principal;

        var roleDto = new RoleDto(
            RoleId.Create().Value,
            "User Admin",
            "User Admin",
            "User administration",
            IsSystemRole: false,
            IsActive: true,
            Permissions: [Permissions.Users.All]);
        this._getUserEffectiveRolesQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)new[] { roleDto });
        this._getGroupClaimsForUserQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<GroupClaimDto>>)Array.Empty<GroupClaimDto>());
        this._scopeManager.ListResourcesAsync(Arg.Any<ImmutableArray<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        IActionResult result = await this._controller.Authorize();

        SignInResult signIn = Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
        IReadOnlyList<string> permissionClaims = signIn.Principal!.FindAll("permission").Select(claim => claim.Value).ToList();
        permissionClaims.ShouldContain(Permissions.Users.Read);
        permissionClaims.ShouldContain(Permissions.Users.Write);
        permissionClaims.ShouldNotContain(Permissions.Users.All);
    }

    [Fact]
    public async Task Authorize_EmitsConcreteDynamicPermissionClaims()
    {
        var userId = Guid.NewGuid();
        var request = new OpenIddictRequest { ClientId = "orders-api" };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            "Cookies"));
        this._controller.ControllerContext.HttpContext.User = principal;

        var roleDto = new RoleDto(
            RoleId.Create().Value,
            "Order Reader",
            "Order Reader",
            "Order permissions",
            IsSystemRole: false,
            IsActive: true,
            Permissions: ["orders-api:order:read"]);
        this._getUserEffectiveRolesQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)new[] { roleDto });
        this._getGroupClaimsForUserQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<GroupClaimDto>>)Array.Empty<GroupClaimDto>());
        this._scopeManager.ListResourcesAsync(Arg.Any<ImmutableArray<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        IActionResult result = await this._controller.Authorize();

        SignInResult signIn = Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
        IReadOnlyList<string> permissionClaims = signIn.Principal!.FindAll("permission").Select(claim => claim.Value).ToList();
        permissionClaims.ShouldBe(["orders-api:order:read"]);
    }

    [Fact]
    public async Task Authorize_ExpandsDynamicWildcardRolePermissionsToConcretePermissionClaims()
    {
        var userId = Guid.NewGuid();
        var request = new OpenIddictRequest { ClientId = "orders-api" };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            "Cookies"));
        this._controller.ControllerContext.HttpContext.User = principal;

        var roleDto = new RoleDto(
            RoleId.Create().Value,
            "Order Admin",
            "Order Admin",
            "Order permissions",
            IsSystemRole: false,
            IsActive: true,
            Permissions: ["orders-api:order:*"]);
        this._getUserEffectiveRolesQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)new[] { roleDto });
        this._getGroupClaimsForUserQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<GroupClaimDto>>)Array.Empty<GroupClaimDto>());
        this._scopeManager.ListResourcesAsync(Arg.Any<ImmutableArray<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 1, 18, 12, 0, 0, TimeSpan.Zero));
        Result<OpenIdentityStack.Domain.ApplicationPermissions.RegisteredApplication> applicationResult =
            OpenIdentityStack.Domain.ApplicationPermissions.RegisteredApplication.Register(
                "orders-api",
                "Orders API",
                null,
                "owner-1",
                OpenIdentityStack.Domain.ApplicationPermissions.OwnerType.User,
                [("order:read", "Read orders", null, null), ("order:write", "Write orders", null, null)],
                "actor-1",
                dateTimeProvider);
        this._applicationPermissionRegistryRepository.GetByIdentifierAsync("orders-api", Arg.Any<CancellationToken>())
            .Returns(applicationResult.Value);

        IActionResult result = await this._controller.Authorize();

        SignInResult signIn = Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
        IReadOnlyList<string> permissionClaims = signIn.Principal!.FindAll("permission").Select(claim => claim.Value).ToList();
        permissionClaims.ShouldBe(["orders-api:order:read", "orders-api:order:write"]);
        permissionClaims.ShouldNotContain("orders-api:order:*");
    }

    [Fact]
    public async Task Authorize_FailsClosedWhenDynamicWildcardCannotBeExpanded()
    {
        var userId = Guid.NewGuid();
        var request = new OpenIddictRequest { ClientId = "orders-api" };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            "Cookies"));
        this._controller.ControllerContext.HttpContext.User = principal;

        var roleDto = new RoleDto(
            RoleId.Create().Value,
            "Order Admin",
            "Order Admin",
            "Order permissions",
            IsSystemRole: false,
            IsActive: true,
            Permissions: ["orders-api:order:*"]);
        this._getUserEffectiveRolesQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)new[] { roleDto });
        this._getGroupClaimsForUserQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<GroupClaimDto>>)Array.Empty<GroupClaimDto>());
        this._scopeManager.ListResourcesAsync(Arg.Any<ImmutableArray<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => this._controller.Authorize());
    }

    [Fact]
    public async Task Authorize_AddsGroupClaimDestinations_WhenTokenTargetBoth()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new OpenIddictRequest { ClientId = "test-client" };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        Claim[] claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        this._controller.ControllerContext.HttpContext.User = principal;

        this._getUserEffectiveRolesQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)Array.Empty<RoleDto>());
        this._getGroupClaimsForUserQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<GroupClaimDto>>)new[]
            {
                new GroupClaimDto("department", "engineering", TokenTarget.Both)
            });
        this._scopeManager.ListResourcesAsync(Arg.Any<ImmutableArray<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        // Act
        IActionResult result = await this._controller.Authorize();

        // Assert
        SignInResult signIn = Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
        Claim claim = signIn.Principal!.Claims.Single(c => c.Type == "department" && c.Value == "engineering");
        Assert.True(claim.Properties.TryGetValue("destinations", out string? destinations));
        Assert.Equal("access_token id_token", destinations);
    }

    #endregion

    #region Exchange Tests

    [Fact]
    public async Task Exchange_WhenRequestIsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns((OpenIddictRequest?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => this._controller.Exchange());
    }

    [Fact]
    public async Task Exchange_ClientCredentials_ReturnsSignIn()
    {
        // Arrange
        var request = new OpenIddictRequest
        {
            ClientId = "test-client",
            GrantType = OpenIddictConstants.GrantTypes.ClientCredentials,
            Scope = "api"
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        object application = new object(); // Mock application object
#pragma warning disable CA2012
        this._applicationManager.FindByClientIdAsync("test-client", Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>(application));
        this._applicationManager.GetClientIdAsync(application, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string?>("test-client"));
        this._applicationManager.GetDisplayNameAsync(application, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string?>("Test Client"));
#pragma warning restore CA2012

        this._scopeManager.ListResourcesAsync(Arg.Any<ImmutableArray<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        // Act
        IActionResult result = await this._controller.Exchange();

        // Assert
        SignInResult signIn = Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
        Assert.Contains(signIn.Principal!.Claims, c => c.Type == OpenIddictConstants.Claims.Subject && c.Value == "test-client");
    }

    [Fact]
    public async Task Exchange_ClientCredentials_WithApiScope_AddsAdminPermission()
    {
        // Arrange
        var request = new OpenIddictRequest
        {
            ClientId = "test-client",
            GrantType = OpenIddictConstants.GrantTypes.ClientCredentials,
            Scope = "api"
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        object application = new object();
#pragma warning disable CA2012
        this._applicationManager.FindByClientIdAsync("test-client", Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>(application));
        this._applicationManager.GetClientIdAsync(application, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string?>("test-client"));
        this._applicationManager.GetDisplayNameAsync(application, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string?>("Test Client"));
#pragma warning restore CA2012

        this._scopeManager.ListResourcesAsync(Arg.Any<ImmutableArray<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        // Act
        IActionResult result = await this._controller.Exchange();

        // Assert
        SignInResult signIn = Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
        Assert.Contains(signIn.Principal!.Claims, c => c.Type == "permission" && c.Value == "*");
    }

    [Fact]
    public async Task Exchange_ClientCredentials_WithoutDisplayName_OmitsNameClaim()
    {
        // Arrange
        var request = new OpenIddictRequest
        {
            ClientId = "test-client",
            GrantType = OpenIddictConstants.GrantTypes.ClientCredentials
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        object application = new object();
#pragma warning disable CA2012
        this._applicationManager.FindByClientIdAsync("test-client", Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>(application));
        this._applicationManager.GetClientIdAsync(application, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string?>("test-client"));
        this._applicationManager.GetDisplayNameAsync(application, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string?>((string?)null));
#pragma warning restore CA2012

        this._scopeManager.ListResourcesAsync(Arg.Any<ImmutableArray<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        // Act
        IActionResult result = await this._controller.Exchange();

        // Assert
        SignInResult signIn = Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
        Assert.DoesNotContain(signIn.Principal!.Claims, c => c.Type == OpenIddictConstants.Claims.Name);
    }

    [Fact]
    public async Task Exchange_ClientCredentials_WhenApplicationMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new OpenIddictRequest
        {
            ClientId = "test-client",
            GrantType = OpenIddictConstants.GrantTypes.ClientCredentials
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

#pragma warning disable CA2012
        this._applicationManager.FindByClientIdAsync("test-client", Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>(result: null));
#pragma warning restore CA2012

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => this._controller.Exchange());
    }

    [Fact]
    public async Task Exchange_AuthorizationCode_WhenAuthFails_ReturnsForbid()
    {
        // Arrange
        var request = new OpenIddictRequest
        {
            GrantType = OpenIddictConstants.GrantTypes.AuthorizationCode
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        this.SetupMockServices(null, authSuccess: false);

        // Act
        IActionResult result = await this._controller.Exchange();

        // Assert
        ForbidResult forbid = Assert.IsType<ForbidResult>(result);
        Assert.Contains(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, forbid.AuthenticationSchemes);
    }

    [Fact]
    public async Task Exchange_AuthorizationCode_WhenAuthSucceeds_ReturnsSignIn()
    {
        // Arrange
        var request = new OpenIddictRequest
        {
            GrantType = OpenIddictConstants.GrantTypes.AuthorizationCode
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        Claim[] claims = new[] { new Claim(OpenIddictConstants.Claims.Subject, "user-123") };
        var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        this.SetupMockServices(principal);

        // Act
        IActionResult result = await this._controller.Exchange();

        // Assert
        Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
    }

    [Fact]
    public async Task Authorize_WhenAuthenticationTicketIssuedUtcIsPresent_CopiesItToOidcPrincipal()
    {
        // Arrange
        var userId = Guid.NewGuid();
        long authTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var request = new OpenIddictRequest
        {
            ClientId = "test-client",
            Scope = OpenIddictConstants.Scopes.OpenId
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        Claim[] claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        this._controller.ControllerContext.HttpContext.User = principal;
        this.SetupMockServices(
            principal,
            properties: new AuthenticationProperties
            {
                IssuedUtc = DateTimeOffset.FromUnixTimeSeconds(authTime)
            });

        this._getUserEffectiveRolesQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)Array.Empty<RoleDto>());
        this._getGroupClaimsForUserQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<GroupClaimDto>>)Array.Empty<GroupClaimDto>());
        this._scopeManager.ListResourcesAsync(Arg.Any<ImmutableArray<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        // Act
        IActionResult result = await this._controller.Authorize();

        // Assert
        SignInResult signIn = Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
        Claim authTimeClaim = signIn.Principal!.Claims.Single(c => c.Type == OpenIddictConstants.Claims.AuthenticationTime);
        Assert.Equal(authTime.ToString(CultureInfo.InvariantCulture), authTimeClaim.Value);
        Assert.Equal(ClaimValueTypes.Integer64, authTimeClaim.ValueType);
        Assert.DoesNotContain(OpenIddictConstants.Destinations.AccessToken, authTimeClaim.GetDestinations());
        Assert.Contains(OpenIddictConstants.Destinations.IdentityToken, authTimeClaim.GetDestinations());
    }

    [Fact]
    public async Task Authorize_PersistsRequestedUserInfoClaims_ForAccessTokenProcessing()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new OpenIddictRequest
        {
            ClientId = "test-client",
            Scope = OpenIddictConstants.Scopes.OpenId
        };
        request[OpenIddictConstants.Parameters.Claims] = JsonDocument.Parse("""
            {
              "userinfo": {
                "name": { "essential": true }
              }
            }
            """).RootElement.Clone();
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        Claim[] claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "John Doe")
        };
        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        this._controller.ControllerContext.HttpContext.User = principal;
        this.SetupMockServices(principal);

        this._getUserEffectiveRolesQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)Array.Empty<RoleDto>());
        this._getGroupClaimsForUserQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<GroupClaimDto>>)Array.Empty<GroupClaimDto>());
        this._scopeManager.ListResourcesAsync(Arg.Any<ImmutableArray<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        // Act
        IActionResult result = await this._controller.Authorize();

        // Assert
        SignInResult signIn = Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
        Claim requestedClaim = signIn.Principal!.Claims.Single(claim =>
            claim.Type == RequestedUserInfoClaim && claim.Value == OpenIddictConstants.Claims.Name);
        requestedClaim.GetDestinations().ShouldBe([OpenIddictConstants.Destinations.AccessToken]);
    }

    [Fact]
    public async Task Authorize_DoesNotExposeProfileOrEmailClaimsInAccessToken_WithoutScopeOrClaimsRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new OpenIddictRequest
        {
            ClientId = "test-client",
            Scope = OpenIddictConstants.Scopes.OpenId
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        Claim[] claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(OpenIddictConstants.Claims.GivenName, "John"),
            new Claim(ClaimTypes.Email, "john@example.com")
        };
        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        this._controller.ControllerContext.HttpContext.User = principal;
        this.SetupMockServices(principal);

        this._getUserEffectiveRolesQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)Array.Empty<RoleDto>());
        this._getGroupClaimsForUserQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<GroupClaimDto>>)Array.Empty<GroupClaimDto>());
        this._scopeManager.ListResourcesAsync(Arg.Any<ImmutableArray<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        // Act
        IActionResult result = await this._controller.Authorize();

        // Assert
        SignInResult signIn = Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
        Claim givenNameClaim = signIn.Principal!.Claims.Single(claim => claim.Type == OpenIddictConstants.Claims.GivenName);
        Claim emailClaim = signIn.Principal.Claims.Single(claim => claim.Type == OpenIddictConstants.Claims.Email);
        Assert.Empty(givenNameClaim.GetDestinations());
        Assert.Empty(emailClaim.GetDestinations());
    }

    [Fact]
    public async Task Authorize_ExposesRequestedProfileOrEmailClaimsInAccessToken_WithoutScope()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new OpenIddictRequest
        {
            ClientId = "test-client",
            Scope = OpenIddictConstants.Scopes.OpenId
        };
        request[OpenIddictConstants.Parameters.Claims] = JsonDocument.Parse("""
            {
              "userinfo": {
                "given_name": { "essential": true },
                "email": { "essential": true }
              }
            }
            """).RootElement.Clone();
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        Claim[] claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(OpenIddictConstants.Claims.GivenName, "John"),
            new Claim(ClaimTypes.Email, "john@example.com")
        };
        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        this._controller.ControllerContext.HttpContext.User = principal;
        this.SetupMockServices(principal);

        this._getUserEffectiveRolesQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)Array.Empty<RoleDto>());
        this._getGroupClaimsForUserQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<GroupClaimDto>>)Array.Empty<GroupClaimDto>());
        this._scopeManager.ListResourcesAsync(Arg.Any<ImmutableArray<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        // Act
        IActionResult result = await this._controller.Authorize();

        // Assert
        SignInResult signIn = Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
        Claim givenNameClaim = signIn.Principal!.Claims.Single(claim => claim.Type == OpenIddictConstants.Claims.GivenName);
        Claim emailClaim = signIn.Principal.Claims.Single(claim => claim.Type == OpenIddictConstants.Claims.Email);
        givenNameClaim.GetDestinations().ShouldBe([OpenIddictConstants.Destinations.AccessToken]);
        emailClaim.GetDestinations().ShouldBe([OpenIddictConstants.Destinations.AccessToken]);
    }

    [Fact]
    public async Task Authorize_AddsPersistedProfileClaims_ForProfileScope()
    {
        // Arrange
        var request = new OpenIddictRequest
        {
            ClientId = "test-client",
            Scope = $"{OpenIddictConstants.Scopes.OpenId} {OpenIddictConstants.Scopes.Profile}"
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        DateTimeOffset now = new(2026, 5, 18, 9, 0, 0, TimeSpan.Zero);
        dateTimeProvider.UtcNow.Returns(now);
        User persistedUser = User.CreateLocal(
            "john@example.com",
            "John Doe",
            "password-hash",
            dateTimeProvider,
            new UserProfileData(
                GivenName: "John",
                FamilyName: "Doe",
                PreferredUsername: "john.doe",
                Profile: "https://profiles.example.test/john.doe",
                Picture: "https://profiles.example.test/john.doe/avatar.svg",
                Website: "https://profiles.example.test/john.doe",
                Gender: "male",
                Birthdate: "1990-01-15",
                ZoneInfo: "Europe/Amsterdam",
                Locale: "en-NL")).Value;

        Claim[] claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, persistedUser.Id.Value.ToString()),
            new Claim(ClaimTypes.Name, "John Doe")
        };
        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        this._controller.ControllerContext.HttpContext.User = principal;
        this.SetupMockServices(principal);

        this._userRepository.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(persistedUser);
        this._getUserEffectiveRolesQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)Array.Empty<RoleDto>());
        this._getGroupClaimsForUserQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<GroupClaimDto>>)Array.Empty<GroupClaimDto>());
        this._scopeManager.ListResourcesAsync(Arg.Any<ImmutableArray<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        // Act
        IActionResult result = await this._controller.Authorize();

        // Assert
        SignInResult signIn = Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
        Assert.Equal("John", signIn.Principal!.GetClaim(OpenIddictConstants.Claims.GivenName));
        Assert.Equal("john.doe", signIn.Principal.GetClaim(OpenIddictConstants.Claims.PreferredUsername));
        Assert.Equal("en-NL", signIn.Principal.GetClaim(OpenIddictConstants.Claims.Locale));
        Assert.Equal(
            now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            signIn.Principal.GetClaim(UpdatedAtClaim));

        Claim givenNameClaim = signIn.Principal.Claims.Single(claim => claim.Type == OpenIddictConstants.Claims.GivenName);
        givenNameClaim.GetDestinations().ShouldBe([
            OpenIddictConstants.Destinations.AccessToken,
            OpenIddictConstants.Destinations.IdentityToken
        ]);
    }

    [Fact]
    public async Task Authorize_PersistsSupportedAcrClaim_ForIdentityTokenProcessing()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new OpenIddictRequest
        {
            ClientId = "test-client",
            Scope = OpenIddictConstants.Scopes.OpenId
        };
        request[OpenIddictConstants.Parameters.AcrValues] = "2 1";
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        Claim[] claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "John Doe")
        };
        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        this._controller.ControllerContext.HttpContext.User = principal;
        this.SetupMockServices(principal);

        this._getUserEffectiveRolesQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)Array.Empty<RoleDto>());
        this._getGroupClaimsForUserQueryHandler.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<GroupClaimDto>>)Array.Empty<GroupClaimDto>());
        this._scopeManager.ListResourcesAsync(Arg.Any<ImmutableArray<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        // Act
        IActionResult result = await this._controller.Authorize();

        // Assert
        SignInResult signIn = Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
        Claim acrClaim = signIn.Principal!.Claims.Single(claim =>
            claim.Type == AcrClaim && claim.Value == SupportedAcrValue);
        acrClaim.GetDestinations().ShouldBe([OpenIddictConstants.Destinations.IdentityToken]);
    }

    [Fact]
    public async Task Exchange_AuthorizationCode_DoesNotAssignPublicDestinationsToInternalStateClaims()
    {
        // Arrange
        var request = new OpenIddictRequest
        {
            GrantType = OpenIddictConstants.GrantTypes.AuthorizationCode
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        Claim[] claims = new[]
        {
            new Claim(OpenIddictConstants.Claims.Subject, "user-123"),
            new Claim("session_id", Guid.NewGuid().ToString()),
            new Claim("oi_tkn_id", "token-id"),
            new Claim("oi_au_id", "authorization-id")
        };

        foreach (Claim claim in claims)
        {
            claim.Properties["destinations"] = "access_token id_token";
        }

        var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        this.SetupMockServices(principal);
        this._validateSessionQueryHandler.HandleAsync(Arg.Any<ValidateSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateSessionResult(true));

        // Act
        IActionResult result = await this._controller.Exchange();

        // Assert
        SignInResult signIn = Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
        Assert.DoesNotContain(signIn.Principal!.Claims, claim => claim.Type is "session_id");

        foreach (Claim claim in signIn.Principal.Claims.Where(claim => claim.Type.StartsWith("oi_", StringComparison.Ordinal)))
        {
            Assert.Empty(claim.GetDestinations());
        }
    }

    [Fact]
    public async Task Exchange_AuthorizationCode_RestoresAuthenticationTimeFromAuthenticationTicket()
    {
        // Arrange
        var request = new OpenIddictRequest
        {
            GrantType = OpenIddictConstants.GrantTypes.AuthorizationCode
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        long authTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Claim[] claims = new[]
        {
            new Claim(OpenIddictConstants.Claims.Subject, "user-123")
        };

        var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(OpenIddictConstants.Scopes.OpenId);
        this.SetupMockServices(
            principal,
            properties: new AuthenticationProperties
            {
                IssuedUtc = DateTimeOffset.FromUnixTimeSeconds(authTime)
            });

        // Act
        IActionResult result = await this._controller.Exchange();

        // Assert
        SignInResult signIn = Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
        Claim authTimeClaim = signIn.Principal!.Claims.Single(c => c.Type == OpenIddictConstants.Claims.AuthenticationTime);
        Assert.Equal(authTime.ToString(CultureInfo.InvariantCulture), authTimeClaim.Value);
        Assert.Equal(ClaimValueTypes.Integer64, authTimeClaim.ValueType);
        Assert.Contains(OpenIddictConstants.Destinations.IdentityToken, authTimeClaim.GetDestinations());
    }

    [Fact]
    public async Task Exchange_RefreshToken_WithInvalidSession_ReturnsForbid()
    {
        // Arrange
        var request = new OpenIddictRequest
        {
            GrantType = OpenIddictConstants.GrantTypes.RefreshToken
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        var sessionId = SessionId.Create();
        Claim[] claims = new[] { new Claim("session_id", sessionId.Value.ToString()) };
        var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        this.SetupMockServices(principal);

        this._validateSessionQueryHandler.HandleAsync(Arg.Is<ValidateSessionQuery>(q => q.SessionId == sessionId), Arg.Any<CancellationToken>())
            .Returns(new ValidateSessionResult(false, "Revoked"));

        // Act
        IActionResult result = await this._controller.Exchange();

        // Assert
        ForbidResult forbid = Assert.IsType<ForbidResult>(result);
        Assert.Contains(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, forbid.AuthenticationSchemes);
    }

    [Fact]
    public async Task Exchange_RefreshToken_WithValidSession_ReturnsSignIn()
    {
        // Arrange
        var request = new OpenIddictRequest
        {
            GrantType = OpenIddictConstants.GrantTypes.RefreshToken
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        var sessionId = SessionId.Create();
        Claim[] claims = new[] { new Claim("session_id", sessionId.Value.ToString()) };
        var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        this.SetupMockServices(principal);

        this._validateSessionQueryHandler.HandleAsync(Arg.Is<ValidateSessionQuery>(q => q.SessionId == sessionId), Arg.Any<CancellationToken>())
            .Returns(new ValidateSessionResult(true));

        // Act
        IActionResult result = await this._controller.Exchange();

        // Assert
        Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
    }

    [Fact]
    public async Task Exchange_RefreshToken_WithInvalidSessionWithoutReason_UsesDefaultMessage()
    {
        // Arrange
        var request = new OpenIddictRequest
        {
            GrantType = OpenIddictConstants.GrantTypes.RefreshToken
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        var sessionId = SessionId.Create();
        Claim[] claims = new[] { new Claim("session_id", sessionId.Value.ToString()) };
        var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        this.SetupMockServices(principal);

        this._validateSessionQueryHandler.HandleAsync(Arg.Is<ValidateSessionQuery>(q => q.SessionId == sessionId), Arg.Any<CancellationToken>())
            .Returns(new ValidateSessionResult(false, null));

        // Act
        IActionResult result = await this._controller.Exchange();

        // Assert
        ForbidResult forbid = Assert.IsType<ForbidResult>(result);
        Assert.NotNull(forbid.Properties);
        Assert.Equal("Session invalid", forbid.Properties!.Items[OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription]);
    }

    [Fact]
    public async Task Exchange_RefreshToken_WithoutSessionClaim_ReturnsSignIn()
    {
        // Arrange
        var request = new OpenIddictRequest
        {
            GrantType = OpenIddictConstants.GrantTypes.RefreshToken
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        // No session_id claim
        Claim[] claims = new[] { new Claim(OpenIddictConstants.Claims.Subject, "user-id") };
        var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        this.SetupMockServices(principal);

        // Act
        IActionResult result = await this._controller.Exchange();

        // Assert - should proceed without session validation
        Assert.IsType<Microsoft.AspNetCore.Mvc.SignInResult>(result);
        await this._validateSessionQueryHandler.DidNotReceive().HandleAsync(Arg.Any<ValidateSessionQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Exchange_UnsupportedGrantType_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new OpenIddictRequest
        {
            GrantType = "unsupported_grant"
        };
        this._requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => this._controller.Exchange());
    }

    #endregion

    #region UserInfo Tests

    [Fact]
    public async Task UserInfo_WhenAuthFails_ReturnsChallenge()
    {
        // Arrange
        this.SetupMockServices(null, authSuccess: false);

        // Act
        IActionResult result = await this._controller.UserInfo();

        // Assert
        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task UserInfo_WhenAuthSucceeds_ReturnsOkWithSubject()
    {
        // Arrange
        Claim[] claims = new[] { new Claim(OpenIddictConstants.Claims.Subject, "user-123") };
        var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        this.SetupMockServices(principal);

        // Act
        IActionResult result = await this._controller.UserInfo();

        // Assert
        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Dictionary<string, object> claims_result = Assert.IsType<Dictionary<string, object>>(ok.Value);
        Assert.Equal("user-123", claims_result[OpenIddictConstants.Claims.Subject]);
    }

    [Fact]
    public async Task UserInfo_IncludesNameClaim_WhenPresent()
    {
        // Arrange
        Claim[] claims = new[]
        {
            new Claim(OpenIddictConstants.Claims.Subject, "user-123"),
            new Claim(OpenIddictConstants.Claims.Name, "John Doe")
        };
        var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(OpenIddictConstants.Scopes.Profile);
        this.SetupMockServices(principal);

        // Act
        IActionResult result = await this._controller.UserInfo();

        // Assert
        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Dictionary<string, object> claims_result = Assert.IsType<Dictionary<string, object>>(ok.Value);
        Assert.Equal("John Doe", claims_result[OpenIddictConstants.Claims.Name]);
    }

    [Fact]
    public async Task UserInfo_IncludesRequestedNameClaim_WithoutProfileScope()
    {
        // Arrange
        Claim[] claims = new[]
        {
            new Claim(OpenIddictConstants.Claims.Subject, "user-123"),
            new Claim(OpenIddictConstants.Claims.Name, "John Doe"),
            new Claim(RequestedUserInfoClaim, OpenIddictConstants.Claims.Name)
        };
        var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(OpenIddictConstants.Scopes.OpenId);
        this.SetupMockServices(principal);

        // Act
        IActionResult result = await this._controller.UserInfo();

        // Assert
        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Dictionary<string, object> claims_result = Assert.IsType<Dictionary<string, object>>(ok.Value);
        Assert.Equal("John Doe", claims_result[OpenIddictConstants.Claims.Name]);
    }

    [Fact]
    public async Task UserInfo_IncludesExtendedProfileClaims_WhenProfileScopeGranted()
    {
        // Arrange
        Claim[] claims = new[]
        {
            new Claim(OpenIddictConstants.Claims.Subject, "user-123"),
            new Claim(OpenIddictConstants.Claims.Name, "John Doe"),
            new Claim(OpenIddictConstants.Claims.GivenName, "John"),
            new Claim(OpenIddictConstants.Claims.FamilyName, "Doe"),
            new Claim(OpenIddictConstants.Claims.PreferredUsername, "john.doe"),
            new Claim(OpenIddictConstants.Claims.Profile, "https://profiles.example.test/john.doe"),
            new Claim(OpenIddictConstants.Claims.Picture, "https://profiles.example.test/john.doe/avatar.svg"),
            new Claim(OpenIddictConstants.Claims.Website, "https://profiles.example.test/john.doe"),
            new Claim(OpenIddictConstants.Claims.Gender, "male"),
            new Claim(OpenIddictConstants.Claims.Birthdate, "1990-01-15"),
            new Claim(OpenIddictConstants.Claims.Zoneinfo, "Europe/Amsterdam"),
            new Claim(OpenIddictConstants.Claims.Locale, "en-NL"),
            new Claim(UpdatedAtClaim, "1716026400", ClaimValueTypes.Integer64)
        };
        var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(OpenIddictConstants.Scopes.Profile);
        this.SetupMockServices(principal);

        // Act
        IActionResult result = await this._controller.UserInfo();

        // Assert
        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Dictionary<string, object> claims_result = Assert.IsType<Dictionary<string, object>>(ok.Value);
        Assert.Equal("John", claims_result[OpenIddictConstants.Claims.GivenName]);
        Assert.Equal("Doe", claims_result[OpenIddictConstants.Claims.FamilyName]);
        Assert.Equal("john.doe", claims_result[OpenIddictConstants.Claims.PreferredUsername]);
        Assert.Equal("en-NL", claims_result[OpenIddictConstants.Claims.Locale]);
        Assert.Equal(1716026400L, claims_result[UpdatedAtClaim]);
    }

    [Fact]
    public async Task UserInfo_IncludesEmailClaim_WhenPresent()
    {
        // Arrange
        Claim[] claims = new[]
        {
            new Claim(OpenIddictConstants.Claims.Subject, "user-123"),
            new Claim(OpenIddictConstants.Claims.Email, "john@example.com")
        };
        var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(OpenIddictConstants.Scopes.Email);
        this.SetupMockServices(principal);

        // Act
        IActionResult result = await this._controller.UserInfo();

        // Assert
        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Dictionary<string, object> claims_result = Assert.IsType<Dictionary<string, object>>(ok.Value);
        Assert.Equal("john@example.com", claims_result[OpenIddictConstants.Claims.Email]);
    }

    [Fact]
    public async Task UserInfo_OmitsNameClaim_WhenNotPresent()
    {
        // Arrange
        Claim[] claims = new[] { new Claim(OpenIddictConstants.Claims.Subject, "user-123") };
        var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        this.SetupMockServices(principal);

        // Act
        IActionResult result = await this._controller.UserInfo();

        // Assert
        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Dictionary<string, object> claims_result = Assert.IsType<Dictionary<string, object>>(ok.Value);
        Assert.False(claims_result.ContainsKey(OpenIddictConstants.Claims.Name));
    }

    [Fact]
    public async Task UserInfo_OmitsNameClaim_WhenProfileScopeWasNotGranted()
    {
        // Arrange
        Claim[] claims = new[]
        {
            new Claim(OpenIddictConstants.Claims.Subject, "user-123"),
            new Claim(OpenIddictConstants.Claims.Name, "John Doe"),
            new Claim(OpenIddictConstants.Claims.Email, "john@example.com")
        };
        var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(OpenIddictConstants.Scopes.Email);
        this.SetupMockServices(principal);

        // Act
        IActionResult result = await this._controller.UserInfo();

        // Assert
        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Dictionary<string, object> claims_result = Assert.IsType<Dictionary<string, object>>(ok.Value);
        Assert.False(claims_result.ContainsKey(OpenIddictConstants.Claims.Name));
        Assert.Equal("john@example.com", claims_result[OpenIddictConstants.Claims.Email]);
    }

    [Fact]
    public async Task UserInfo_OmitsEmailClaims_WhenEmailScopeWasNotGranted()
    {
        // Arrange
        Claim[] claims = new[]
        {
            new Claim(OpenIddictConstants.Claims.Subject, "user-123"),
            new Claim(OpenIddictConstants.Claims.Name, "John Doe"),
            new Claim(OpenIddictConstants.Claims.Email, "john@example.com"),
            new Claim(OpenIddictConstants.Claims.EmailVerified, "true", ClaimValueTypes.Boolean)
        };
        var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(OpenIddictConstants.Scopes.Profile);
        this.SetupMockServices(principal);

        // Act
        IActionResult result = await this._controller.UserInfo();

        // Assert
        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Dictionary<string, object> claims_result = Assert.IsType<Dictionary<string, object>>(ok.Value);
        Assert.Equal("John Doe", claims_result[OpenIddictConstants.Claims.Name]);
        Assert.False(claims_result.ContainsKey(OpenIddictConstants.Claims.Email));
        Assert.False(claims_result.ContainsKey(OpenIddictConstants.Claims.EmailVerified));
    }

    #endregion
}
