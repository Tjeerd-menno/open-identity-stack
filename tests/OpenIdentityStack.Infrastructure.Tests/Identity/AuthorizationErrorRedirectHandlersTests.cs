using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIdentityStack.Infrastructure.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace OpenIdentityStack.Infrastructure.Tests.Identity;

public sealed class RedirectUnsupportedRequestParameterErrorsHandlerTests
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly RedirectUnsupportedRequestParameterErrorsHandler _handler;

    public RedirectUnsupportedRequestParameterErrorsHandlerTests()
    {
        _applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        _handler = new RedirectUnsupportedRequestParameterErrorsHandler(_applicationManager);
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

#pragma warning disable CA2012
        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>(application));
        _applicationManager.ValidateRedirectUriAsync(application, redirectUri, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(true));
#pragma warning restore CA2012

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

#pragma warning disable CA2012
        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>(application));
        _applicationManager.ValidateRedirectUriAsync(application, redirectUri, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(false));
#pragma warning restore CA2012

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.RedirectUri.ShouldBeNull();
        context.Response[Parameters.State].ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_DoesNothing_WhenErrorIsDifferent()
    {
        // Arrange
        ApplyAuthorizationResponseContext context = CreateContext(
            "oidf-code-client",
            "https://client.example.com/callback",
            CreateUnsignedRequestObject("state-123"),
            error: Errors.InvalidRequest);

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
        RedirectUnsupportedRequestParameterErrorsHandler.Descriptor.Order
            .ShouldBeGreaterThan(OpenIddictServerHandlers.Authentication
                .ApplyAuthorizationResponse<ApplyAuthorizationResponseContext>
                .Descriptor.Order);
    }

    [Fact]
    public void Descriptor_IsCustomHandlerType()
    {
        RedirectUnsupportedRequestParameterErrorsHandler.Descriptor.Type
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

    private static string CreateUnsignedRequestObject(string state)
    {
        static string Encode(string json) =>
            WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(json));

        string header = Encode("""{"alg":"none"}""");
        string payload = Encode($$"""{"client_id":"oidf-code-client","redirect_uri":"https://client.example.com/callback","response_type":"code","scope":"openid","state":"{{state}}"}""");
        return $"{header}.{payload}.";
    }
}
