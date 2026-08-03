using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using OpenIdentityStack.Infrastructure.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

#pragma warning disable CA2012 // NSubstitute setup captures ValueTask-returning calls.

namespace OpenIdentityStack.Infrastructure.Tests.Identity;

public sealed class AuthorizationErrorRedirectHelperTests
{
    private readonly IOpenIddictApplicationManager _applicationManager;

    public AuthorizationErrorRedirectHelperTests()
    {
        _applicationManager = Substitute.For<IOpenIddictApplicationManager>();
    }

    [Fact]
    public async Task TryBuildRedirectLocation_ReturnsNull_WhenStatusIsNot400()
    {
        // Arrange
        HttpContext context = CreateHttpContext(
            StatusCodes.Status200OK,
            "client-id",
            "https://client.example.com/callback",
            Errors.InvalidRequest);

        // Act
        string? result = await AuthorizationErrorRedirectHelper.TryBuildRedirectLocationAsync(context, _applicationManager);

        // Assert
        result.ShouldBeNull();
        await _applicationManager.DidNotReceive().FindByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryBuildRedirectLocation_ReturnsNull_WhenNoError()
    {
        // Arrange – a successful response (no error) must not redirect
        HttpContext context = CreateHttpContext(
            StatusCodes.Status400BadRequest,
            "client-id",
            "https://client.example.com/callback",
            error: null);

        // Act
        string? result = await AuthorizationErrorRedirectHelper.TryBuildRedirectLocationAsync(context, _applicationManager);

        // Assert
        result.ShouldBeNull();
        await _applicationManager.DidNotReceive().FindByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryBuildRedirectLocation_ReturnsNull_WhenRedirectUriIsMissing()
    {
        // Arrange – missing redirect_uri means we cannot safely redirect
        HttpContext context = CreateHttpContext(
            StatusCodes.Status400BadRequest,
            "client-id",
            redirectUri: null,
            Errors.InvalidRequest);

        // Act
        string? result = await AuthorizationErrorRedirectHelper.TryBuildRedirectLocationAsync(context, _applicationManager);

        // Assert
        result.ShouldBeNull();
        await _applicationManager.DidNotReceive().FindByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryBuildRedirectLocation_ReturnsNull_WhenRedirectUriIsNotValidated()
    {
        // Arrange
        const string clientId = "client-id";
        const string redirectUri = "https://client.example.com/callback";
        object application = new();

        HttpContext context = CreateHttpContext(
            StatusCodes.Status400BadRequest,
            clientId,
            redirectUri,
            Errors.InvalidRequest);

        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<object?>(application));
        _applicationManager.ValidateRedirectUriAsync(application, redirectUri, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(false));

        // Act
        string? result = await AuthorizationErrorRedirectHelper.TryBuildRedirectLocationAsync(context, _applicationManager);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task TryBuildRedirectLocation_ReturnsRedirectLocation_ForInvalidRequest()
    {
        // Arrange – invalid_request (e.g. bad code_challenge_method) must redirect per OIDC Core §3.1.2.6
        const string clientId = "client-id";
        const string redirectUri = "https://client.example.com/callback";
        const string state = "state-456";
        object application = new();

        HttpContext context = CreateHttpContext(
            StatusCodes.Status400BadRequest,
            clientId,
            redirectUri,
            Errors.InvalidRequest,
            state: state);

        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<object?>(application));
        _applicationManager.ValidateRedirectUriAsync(application, redirectUri, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(true));

        // Act
        string? result = await AuthorizationErrorRedirectHelper.TryBuildRedirectLocationAsync(context, _applicationManager);

        // Assert
        result.ShouldNotBeNull();
        var parsed = new Uri(result);
        QueryHelpers.ParseQuery(parsed.Query)[Parameters.Error].ToString().ShouldBe(Errors.InvalidRequest);
        QueryHelpers.ParseQuery(parsed.Query)[Parameters.State].ToString().ShouldBe(state);
    }

    [Fact]
    public async Task TryBuildRedirectLocation_ReturnsRedirectLocation_ForRequestNotSupported()
    {
        // Arrange – request_not_supported (original behavior) must still redirect
        const string clientId = "client-id";
        const string redirectUri = "https://client.example.com/callback";
        const string state = "state-123";
        object application = new();

        HttpContext context = CreateHttpContext(
            StatusCodes.Status400BadRequest,
            clientId,
            redirectUri,
            Errors.RequestNotSupported,
            state: state,
            requestObject: CreateUnsignedRequestObject(state));

        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<object?>(application));
        _applicationManager.ValidateRedirectUriAsync(application, redirectUri, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(true));

        // Act
        string? result = await AuthorizationErrorRedirectHelper.TryBuildRedirectLocationAsync(context, _applicationManager);

        // Assert
        result.ShouldNotBeNull();
        var parsed = new Uri(result);
        QueryHelpers.ParseQuery(parsed.Query)[Parameters.Error].ToString().ShouldBe(Errors.RequestNotSupported);
    }

    [Fact]
    public async Task TryBuildRedirectLocation_ReturnsRedirectLocation_WithoutJwtRequestObject()
    {
        // Arrange – plain (non-JWT) authorization requests must also redirect
        const string clientId = "client-id";
        const string redirectUri = "https://client.example.com/callback";
        const string state = "state-789";
        object application = new();

        HttpContext context = CreateHttpContext(
            StatusCodes.Status400BadRequest,
            clientId,
            redirectUri,
            Errors.InvalidRequest,
            state: state,
            requestObject: null);

        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<object?>(application));
        _applicationManager.ValidateRedirectUriAsync(application, redirectUri, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(true));

        // Act
        string? result = await AuthorizationErrorRedirectHelper.TryBuildRedirectLocationAsync(context, _applicationManager);

        // Assert
        result.ShouldNotBeNull();
        var parsed = new Uri(result);
        QueryHelpers.ParseQuery(parsed.Query)[Parameters.Error].ToString().ShouldBe(Errors.InvalidRequest);
        QueryHelpers.ParseQuery(parsed.Query)[Parameters.State].ToString().ShouldBe(state);
    }

    private static HttpContext CreateHttpContext(
        int statusCode,
        string? clientId,
        string? redirectUri,
        string? error,
        string? state = null,
        string? requestObject = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.StatusCode = statusCode;

        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest
            {
                ClientId = clientId,
                RedirectUri = redirectUri,
                State = state,
                Request = requestObject
            },
            Response = new OpenIddictResponse
            {
                Error = error
            }
        };

        var feature = new OpenIddictServerAspNetCoreFeature { Transaction = transaction };
        httpContext.Features.Set(feature);

        return httpContext;
    }

    private static string CreateUnsignedRequestObject(string state)
    {
        static string Encode(string json) =>
            WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(json));

        string header = Encode("""{"alg":"none"}""");
        string payload = Encode($$"""{"client_id":"client-id","redirect_uri":"https://client.example.com/callback","response_type":"code","scope":"openid","state":"{{state}}"}""");
        return $"{header}.{payload}.";
    }
}
