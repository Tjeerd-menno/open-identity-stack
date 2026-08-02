using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIdentityStack.Infrastructure.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

#pragma warning disable CA2012 // NSubstitute setup captures ValueTask-returning calls.

namespace OpenIdentityStack.Infrastructure.Tests.Identity;

public sealed class RedirectAuthorizationErrorsHandlerTests
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly RedirectAuthorizationErrorsHandler _handler;

    public RedirectAuthorizationErrorsHandlerTests()
    {
        _applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        _handler = new RedirectAuthorizationErrorsHandler(_applicationManager);
    }

    [Fact]
    public async Task HandleAsync_RedirectsToValidatedClientAndRestoresState()
    {
        // Arrange
        const string clientId = "oidf-code-client";
        const string redirectUri = "https://client.example.com/callback";
        const string state = "state-123";
        object application = new();

        ApplyAuthorizationResponseContext context = CreateContext(
            clientId,
            redirectUri,
            CreateUnsignedRequestObject(state));

        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(application));
        _applicationManager.ValidateRedirectUriAsync(application, redirectUri, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.RedirectUri.ShouldBe(redirectUri);
        context.ResponseMode.ShouldBe(ResponseModes.Query);
        context.Response[Parameters.State]?.ToString().ShouldBe(state);
    }

    [Fact]
    public async Task HandleAsync_RedirectsForInvalidRequest_WhenClientAndRedirectUriAreValid()
    {
        // Arrange – invalid_request (e.g. bad code_challenge_method) must redirect per OIDC Core §3.1.2.6
        const string clientId = "oidf-code-client";
        const string redirectUri = "https://client.example.com/callback";
        const string state = "state-456";
        object application = new();

        ApplyAuthorizationResponseContext context = CreateContext(
            clientId,
            redirectUri,
            CreateUnsignedRequestObject(state),
            error: Errors.InvalidRequest);

        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(application));
        _applicationManager.ValidateRedirectUriAsync(application, redirectUri, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.RedirectUri.ShouldBe(redirectUri);
        context.ResponseMode.ShouldBe(ResponseModes.Query);
    }

    [Fact]
    public async Task HandleAsync_RedirectsWithoutJwtRequestObject_WhenClientAndRedirectUriAreValid()
    {
        // Arrange – plain (non-JWT) authorization requests must also be redirected
        const string clientId = "oidf-code-client";
        const string redirectUri = "https://client.example.com/callback";
        const string state = "state-789";
        object application = new();

        ApplyAuthorizationResponseContext context = CreateContextWithoutRequestObject(
            clientId,
            redirectUri,
            state,
            error: Errors.InvalidRequest);

        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(application));
        _applicationManager.ValidateRedirectUriAsync(application, redirectUri, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.RedirectUri.ShouldBe(redirectUri);
        context.ResponseMode.ShouldBe(ResponseModes.Query);
        context.Response[Parameters.State]?.ToString().ShouldBe(state);
    }

    [Fact]
    public async Task HandleAsync_DoesNothing_WhenRedirectUriIsNotValidated()
    {
        // Arrange
        const string clientId = "oidf-code-client";
        const string redirectUri = "https://client.example.com/callback";
        object application = new();

        ApplyAuthorizationResponseContext context = CreateContext(
            clientId,
            redirectUri,
            CreateUnsignedRequestObject("state-123"));

        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(application));
        _applicationManager.ValidateRedirectUriAsync(application, redirectUri, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(false));

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.RedirectUri.ShouldBeNull();
        context.Response[Parameters.State].ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_DoesNothing_WhenNoError()
    {
        // Arrange – a successful response (no error) must not trigger a redirect
        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest
            {
                ClientId = "oidf-code-client",
                RedirectUri = "https://client.example.com/callback"
            },
            Response = new OpenIddictResponse()
        };
        var context = new ApplyAuthorizationResponseContext(transaction);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.RedirectUri.ShouldBeNull();
        await _applicationManager.DidNotReceive()
            .FindByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DoesNothing_WhenRedirectUriIsMissing()
    {
        // Arrange – missing redirect_uri means we cannot safely redirect
        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest
            {
                ClientId = "oidf-code-client"
            },
            Response = new OpenIddictResponse
            {
                Error = Errors.InvalidRequest
            }
        };
        var context = new ApplyAuthorizationResponseContext(transaction);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.RedirectUri.ShouldBeNull();
        await _applicationManager.DidNotReceive()
            .FindByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Descriptor_HasCorrectOrder()
    {
        RedirectAuthorizationErrorsHandler.Descriptor.Order
            .ShouldBeGreaterThan(OpenIddictServerHandlers.Authentication
                .ApplyAuthorizationResponse<ApplyAuthorizationResponseContext>
                .Descriptor.Order);
    }

    [Fact]
    public void Descriptor_IsCustomHandlerType()
    {
        RedirectAuthorizationErrorsHandler.Descriptor.Type
            .ShouldBe(OpenIddictServerHandlerType.Custom);
    }

    private static ApplyAuthorizationResponseContext CreateContext(
        string clientId,
        string redirectUri,
        string requestObject,
        string error = Errors.RequestNotSupported)
    {
        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest
            {
                ClientId = clientId,
                RedirectUri = redirectUri,
                Request = requestObject
            },
            Response = new OpenIddictResponse
            {
                Error = error,
                ErrorDescription = "The 'request' parameter is not supported."
            }
        };

        return new ApplyAuthorizationResponseContext(transaction);
    }

    private static ApplyAuthorizationResponseContext CreateContextWithoutRequestObject(
        string clientId,
        string redirectUri,
        string state,
        string error = Errors.InvalidRequest)
    {
        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest
            {
                ClientId = clientId,
                RedirectUri = redirectUri,
                State = state
            },
            Response = new OpenIddictResponse
            {
                Error = error,
                ErrorDescription = "One or more parameters are invalid."
            }
        };

        return new ApplyAuthorizationResponseContext(transaction);
    }

    private static string CreateUnsignedRequestObject(string state)
    {
        static string Encode(string json) =>
            WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(json));

        string header = Encode("""{"alg":"none"}""");
        string payload = Encode($$"""{"client_id":"oidf-code-client","redirect_uri":"https://client.example.com/callback","response_type":"code","scope":"openid","state":"{{state}}"}""");
        return $"{header}.{payload}.";
    }
}
