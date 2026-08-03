using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using OpenIdentityStack.Infrastructure.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

#pragma warning disable CA2012 // NSubstitute setup captures ValueTask-returning calls.

namespace OpenIdentityStack.Infrastructure.Tests.Identity;

public sealed class AuthorizationErrorRedirectMiddlewareTests
{
    private const string ClientId = "oidf-code-client";
    private const string RedirectUri = "https://client.example.com/callback";

    private readonly IOpenIddictApplicationManager _applicationManager = Substitute.For<IOpenIddictApplicationManager>();

    [Fact]
    public async Task InvokeAsync_RedirectsInvalidRequest_WhenClientAndRedirectUriAreValid()
    {
        // Arrange – invalid_request (e.g. a bad code_challenge_method) must redirect per OIDC Core §3.1.2.6
        AllowClient();
        DefaultHttpContext context = CreateAuthorizationContext(new OpenIddictRequest
        {
            ClientId = ClientId,
            RedirectUri = RedirectUri,
            ResponseType = ResponseTypes.Code,
            State = "state-456"
        });

        // Act
        await InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status302Found);
        string location = context.Response.Headers.Location.ToString();
        location.ShouldStartWith(RedirectUri + "?");
        location.ShouldContain("error=invalid_request");
        location.ShouldContain("state=state-456");
    }

    [Fact]
    public async Task InvokeAsync_RestoresStateFromJwtRequestObject()
    {
        // Arrange
        AllowClient();
        DefaultHttpContext context = CreateAuthorizationContext(new OpenIddictRequest
        {
            ClientId = ClientId,
            RedirectUri = RedirectUri,
            ResponseType = ResponseTypes.Code,
            Request = CreateUnsignedRequestObject("state-from-jwt")
        });

        // Act
        await InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status302Found);
        context.Response.Headers.Location.ToString().ShouldContain("state=state-from-jwt");
    }

    [Fact]
    public async Task InvokeAsync_UsesFormPost_WhenRequestedResponseModeIsFormPost()
    {
        // Arrange – a form_post client never consumes a query redirect, so the error must be posted back
        AllowClient();
        DefaultHttpContext context = CreateAuthorizationContext(new OpenIddictRequest
        {
            ClientId = ClientId,
            RedirectUri = RedirectUri,
            ResponseType = ResponseTypes.Code,
            ResponseMode = ResponseModes.FormPost,
            State = "state-form-post"
        });

        // Act
        await InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        context.Response.ContentType.ShouldBe("text/html;charset=UTF-8");
        context.Response.Headers.Location.ToString().ShouldBeEmpty();

        string body = ReadBody(context);
        body.ShouldContain($"""<form method="post" action="{RedirectUri}">""");
        body.ShouldContain("""<input type="hidden" name="error" value="invalid_request"/>""");
        body.ShouldContain("""<input type="hidden" name="state" value="state-form-post"/>""");
    }

    [Fact]
    public async Task InvokeAsync_UsesFragment_WhenRequestedResponseModeIsFragment()
    {
        // Arrange
        AllowClient();
        DefaultHttpContext context = CreateAuthorizationContext(new OpenIddictRequest
        {
            ClientId = ClientId,
            RedirectUri = RedirectUri,
            ResponseType = ResponseTypes.Code,
            ResponseMode = ResponseModes.Fragment,
            State = "state-fragment"
        });

        // Act
        await InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status302Found);
        string location = context.Response.Headers.Location.ToString();
        location.ShouldStartWith(RedirectUri + "#");
        location.ShouldContain("error=invalid_request");
        location.ShouldContain("state=state-fragment");
        location.ShouldNotContain("?");
    }

    [Fact]
    public async Task InvokeAsync_DefaultsToFragment_ForFrontChannelResponseTypes()
    {
        // Arrange – implicit and hybrid flows default to the fragment encoding
        AllowClient();
        DefaultHttpContext context = CreateAuthorizationContext(new OpenIddictRequest
        {
            ClientId = ClientId,
            RedirectUri = RedirectUri,
            ResponseType = $"{ResponseTypes.Code} {ResponseTypes.IdToken}",
            State = "state-hybrid"
        });

        // Act
        await InvokeAsync(context);

        // Assert
        context.Response.Headers.Location.ToString().ShouldStartWith(RedirectUri + "#");
    }

    [Fact]
    public async Task InvokeAsync_LeavesErrorPage_WhenResponseModeIsUnsupported()
    {
        // Arrange – silently downgrading to a query redirect would deliver the error
        // somewhere the client is not listening
        AllowClient();
        DefaultHttpContext context = CreateAuthorizationContext(new OpenIddictRequest
        {
            ClientId = ClientId,
            RedirectUri = RedirectUri,
            ResponseType = ResponseTypes.Code,
            ResponseMode = "web_message"
        });

        // Act
        await InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ReadBody(context).ShouldBe("error page");
    }

    [Fact]
    public async Task InvokeAsync_LeavesErrorPage_WhenRedirectUriIsMissing()
    {
        // Arrange – missing redirect_uri means we cannot safely redirect
        DefaultHttpContext context = CreateAuthorizationContext(new OpenIddictRequest
        {
            ClientId = ClientId,
            ResponseType = ResponseTypes.Code
        });

        // Act
        await InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ReadBody(context).ShouldBe("error page");
        await _applicationManager.DidNotReceive()
            .FindByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_LeavesErrorPage_WhenRedirectUriIsNotRegistered()
    {
        // Arrange
        object application = new();
        _applicationManager.FindByClientIdAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(application));
        _applicationManager.ValidateRedirectUriAsync(application, RedirectUri, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(false));

        DefaultHttpContext context = CreateAuthorizationContext(new OpenIddictRequest
        {
            ClientId = ClientId,
            RedirectUri = RedirectUri,
            ResponseType = ResponseTypes.Code
        });

        // Act
        await InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ReadBody(context).ShouldBe("error page");
    }

    [Fact]
    public async Task InvokeAsync_LeavesResponseUntouched_WhenThereIsNoError()
    {
        // Arrange – successful responses are never rewritten
        AllowClient();
        DefaultHttpContext context = CreateAuthorizationContext(
            new OpenIddictRequest
            {
                ClientId = ClientId,
                RedirectUri = RedirectUri,
                ResponseType = ResponseTypes.Code
            },
            new OpenIddictResponse(),
            statusCode: StatusCodes.Status200OK,
            body: "consent page");

        // Act
        await InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        ReadBody(context).ShouldBe("consent page");
        await _applicationManager.DidNotReceive()
            .FindByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_SkipsNonAuthorizationEndpoints()
    {
        // Arrange
        AllowClient();
        DefaultHttpContext context = CreateAuthorizationContext(new OpenIddictRequest
        {
            ClientId = ClientId,
            RedirectUri = RedirectUri,
            ResponseType = ResponseTypes.Code
        });
        context.Request.Path = "/connect/token";

        // Act
        await InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ReadBody(context).ShouldBe("error page");
    }

    private void AllowClient()
    {
        object application = new();
        _applicationManager.FindByClientIdAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(application));
        _applicationManager.ValidateRedirectUriAsync(application, RedirectUri, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
    }

    private Task InvokeAsync(HttpContext context)
    {
        var middleware = new AuthorizationErrorRedirectMiddleware(async downstream =>
        {
            HttpResponse response = downstream.Response;
            response.StatusCode = (int)downstream.Items["status"]!;
            await response.WriteAsync((string)downstream.Items["body"]!);
        });

        return middleware.InvokeAsync(context, _applicationManager);
    }

    private static DefaultHttpContext CreateAuthorizationContext(
        OpenIddictRequest request,
        OpenIddictResponse? response = null,
        int statusCode = StatusCodes.Status400BadRequest,
        string body = "error page")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/connect/authorize";
        context.Response.Body = new MemoryStream();
        context.Items["status"] = statusCode;
        context.Items["body"] = body;

        context.Features.Set(new OpenIddictServerAspNetCoreFeature
        {
            Transaction = new OpenIddictServerTransaction
            {
                Request = request,
                Response = response ?? new OpenIddictResponse
                {
                    Error = Errors.InvalidRequest,
                    ErrorDescription = "One or more parameters are invalid."
                }
            }
        });

        return context;
    }

    private static string ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static string CreateUnsignedRequestObject(string state)
    {
        static string Encode(string json) =>
            WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(json));

        string header = Encode("""{"alg":"none"}""");
        string payload = Encode($$"""{"client_id":"{{ClientId}}","redirect_uri":"{{RedirectUri}}","response_type":"code","scope":"openid","state":"{{state}}"}""");
        return $"{header}.{payload}.";
    }
}
