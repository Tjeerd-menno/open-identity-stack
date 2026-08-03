using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using OpenIdentityStack.Infrastructure.Identity;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace OpenIdentityStack.Infrastructure.Tests.Identity;

public sealed class AddCheckSessionMetadataHandlerTests
{
    private readonly AddCheckSessionMetadataHandler _handler;

    public AddCheckSessionMetadataHandlerTests()
    {
        this._handler = new AddCheckSessionMetadataHandler();
    }

    [Fact]
    public async Task HandleAsync_AddsCheckSessionIframe_WhenIssuerIsSet()
    {
        // Arrange
        HandleConfigurationRequestContext context = CreateContext();
        context.Issuer = new Uri("https://example.com");

        // Act
        await this._handler.HandleAsync(context);

        // Assert
        context.Metadata.ShouldContainKey("check_session_iframe");
        context.Metadata["check_session_iframe"].ShouldBe("https://example.com/connect/check_session");
    }

    [Fact]
    public async Task HandleAsync_DoesNotAddCheckSessionIframe_WhenIssuerIsNull()
    {
        // Arrange
        HandleConfigurationRequestContext context = CreateContext();
        context.Issuer = null;

        // Act
        await this._handler.HandleAsync(context);

        // Assert
        context.Metadata.ShouldNotContainKey("check_session_iframe");
    }

    [Fact]
    public void Descriptor_HasCorrectOrder()
    {
        // Assert
        AddCheckSessionMetadataHandler.Descriptor.Order
            .ShouldBeGreaterThan(OpenIddictServerHandlers.Discovery.HandleConfigurationRequest.Descriptor.Order);
    }

    [Fact]
    public void Descriptor_IsCustomHandlerType()
    {
        // Assert
        AddCheckSessionMetadataHandler.Descriptor.Type.ShouldBe(OpenIddictServerHandlerType.Custom);
    }

    private static HandleConfigurationRequestContext CreateContext()
    {
        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest()
        };

        transaction.Properties[typeof(HttpContext).FullName!] = new DefaultHttpContext();

        return new HandleConfigurationRequestContext(transaction);
    }
}

public sealed class AttachSessionStateHandlerTests
{
    private readonly AttachSessionStateHandler _handler;

    public AttachSessionStateHandlerTests()
    {
        this._handler = new AttachSessionStateHandler();
    }

    [Fact]
    public async Task HandleAsync_AttachesSessionState_WhenAllConditionsMet()
    {
        // Arrange
        ApplyAuthorizationResponseContext context = CreateContext("test-client", "https://client.example.com/callback", "test-session-value");

        // Act
        await this._handler.HandleAsync(context);

        // Assert
        context.Response.ShouldNotBeNull();
        context.Response[SessionManagementConstants.SessionState].ShouldNotBeNull();
        string? sessionState = context.Response[SessionManagementConstants.SessionState]?.ToString();
        sessionState.ShouldNotBeNullOrWhiteSpace();
        sessionState.ShouldContain(".");
    }

    [Fact]
    public async Task HandleAsync_SessionStateContainsSalt()
    {
        // Arrange
        ApplyAuthorizationResponseContext context = CreateContext("test-client", "https://client.example.com/callback", "test-session-value");

        // Act
        await this._handler.HandleAsync(context);

        // Assert
        string? sessionState = context.Response[SessionManagementConstants.SessionState]?.ToString();
        string[] parts = sessionState!.Split('.');
        parts.Length.ShouldBe(2);
        parts[0].ShouldNotBeNullOrWhiteSpace(); // digest
        parts[1].ShouldNotBeNullOrWhiteSpace(); // salt
    }

    [Fact]
    public async Task HandleAsync_DoesNotAttachSessionState_WhenRequestIsNull()
    {
        // Arrange
        var transaction = new OpenIddictServerTransaction();
        var context = new ApplyAuthorizationResponseContext(transaction)
        {
            Response = new OpenIddictResponse()
        };

        // Act
        await this._handler.HandleAsync(context);

        // Assert
        context.Response[SessionManagementConstants.SessionState].ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_DoesNotAttachSessionState_WhenClientIdMissing()
    {
        // Arrange
        ApplyAuthorizationResponseContext context = CreateContext(null, "https://client.example.com/callback", "test-session-value");

        // Act
        await this._handler.HandleAsync(context);

        // Assert
        context.Response[SessionManagementConstants.SessionState].ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_DoesNotAttachSessionState_WhenRedirectUriMissing()
    {
        // Arrange
        ApplyAuthorizationResponseContext context = CreateContext("test-client", null, "test-session-value");

        // Act
        await this._handler.HandleAsync(context);

        // Assert
        context.Response[SessionManagementConstants.SessionState].ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_DoesNotAttachSessionState_WhenSessionCookieMissing()
    {
        // Arrange
        ApplyAuthorizationResponseContext context = CreateContext("test-client", "https://client.example.com/callback", null);

        // Act
        await this._handler.HandleAsync(context);

        // Assert
        context.Response[SessionManagementConstants.SessionState].ShouldBeNull();
    }

    [Fact]
    public void Descriptor_HasCorrectOrder()
    {
        // Assert
        int processSelfRedirectionOrder = OpenIddictServerAspNetCoreHandlers.Authentication.ProcessSelfRedirection.Descriptor.Order;
        int processFormPostResponseOrder = OpenIddictServerAspNetCoreHandlers.Authentication.ProcessFormPostResponse.Descriptor.Order;

        AttachSessionStateHandler.Descriptor.Order.ShouldBe(processSelfRedirectionOrder - SessionManagementConstants.CustomHandlerOrderOffset);
        AttachSessionStateHandler.Descriptor.Order.ShouldBeLessThan(processFormPostResponseOrder);
    }

    [Fact]
    public void Descriptor_IsCustomHandlerType()
    {
        // Assert
        AttachSessionStateHandler.Descriptor.Type.ShouldBe(OpenIddictServerHandlerType.Custom);
    }

    private static ApplyAuthorizationResponseContext CreateContext(string? clientId, string? redirectUri, string? sessionCookie)
    {
        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest
            {
                ClientId = clientId,
                RedirectUri = redirectUri
            },
            Response = new OpenIddictResponse()
        };

        var httpContext = new DefaultHttpContext();
        
        // Set up request cookies using the features collection
        if (sessionCookie != null)
        {
            IHttpRequestFeature? requestFeature = httpContext.Features.Get<IHttpRequestFeature>();
            if (requestFeature != null)
            {
                requestFeature.Headers["Cookie"] = $"{SessionManagementDefaults.SessionCookieName}={sessionCookie}";
            }
        }

        transaction.Properties[typeof(HttpContext).FullName!] = httpContext;

        return new ApplyAuthorizationResponseContext(transaction);
    }
}
