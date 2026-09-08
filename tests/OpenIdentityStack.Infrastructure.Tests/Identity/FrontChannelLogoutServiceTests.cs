
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Infrastructure.Identity;

namespace OpenIdentityStack.Infrastructure.Tests.Identity;
/// <summary>
/// Unit tests for FrontChannelLogoutService.
/// </summary>
public sealed class FrontChannelLogoutServiceTests
{
    private readonly ILogger<FrontChannelLogoutService> _logger;
    private readonly FrontChannelLogoutService _service;

    public FrontChannelLogoutServiceTests()
    {
        this._logger = Substitute.For<ILogger<FrontChannelLogoutService>>();
        this._service = this.CreateService("https://identity.example.test");
    }

    [Fact]
    public void GenerateLogoutFrames_ClientWithFrontChannelUri_GeneratesFrame()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client-1", "https://client1.com/logout", null)
        };

        // Act
        IReadOnlyList<FrontChannelLogoutFrame> frames = this._service.GenerateLogoutFrames(sessionId, clients);

        // Assert
        frames.ShouldHaveSingleItem();
        frames[0].ClientId.ShouldBe("client-1");
        frames[0].IframeUrl.ShouldStartWith("https://client1.com/logout");
    }

    [Fact]
    public void GenerateLogoutFrames_ClientWithoutFrontChannelUri_SkipsClient()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client-1", null, "https://client1.com/backchannel")
        };

        // Act
        IReadOnlyList<FrontChannelLogoutFrame> frames = this._service.GenerateLogoutFrames(sessionId, clients);

        // Assert
        frames.ShouldBeEmpty();
    }

    [Fact]
    public void GenerateLogoutFrames_IncludesSessionId()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client-1", "https://client1.com/logout", null)
        };

        // Act
        IReadOnlyList<FrontChannelLogoutFrame> frames = this._service.GenerateLogoutFrames(sessionId, clients);

        // Assert
        frames.ShouldHaveSingleItem();
        frames[0].IframeUrl.ShouldContain($"sid={sessionId.Value}");
    }

    [Fact]
    public void GenerateLogoutFrames_IncludesIssuer()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client-1", "https://client1.com/logout", null)
        };

        // Act
        IReadOnlyList<FrontChannelLogoutFrame> frames = this._service.GenerateLogoutFrames(sessionId, clients);

        // Assert
        frames.ShouldHaveSingleItem();
        frames[0].IframeUrl.ShouldContain("iss=");
    }

    [Fact]
    public void GenerateLogoutFrames_UsesConfiguredIssuerAndPreservesUriFragment()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("request-derived.example.test");
        FrontChannelLogoutService service = this.CreateService("https://identity.example.test", context);
        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client-1", "https://client1.com/logout?return=true#complete", null)
        };

        // Act
        IReadOnlyList<FrontChannelLogoutFrame> frames = service.GenerateLogoutFrames(sessionId, clients);

        // Assert
        frames.ShouldHaveSingleItem();
        frames[0].IframeUrl.ShouldContain("iss=https%3A%2F%2Fidentity.example.test");
        frames[0].IframeUrl.ShouldEndWith("#complete");
        frames[0].IframeUrl.ShouldContain($"sid={sessionId.Value}");
    }

    [Fact]
    public void GenerateLogoutFrames_WithoutConfiguredIssuer_UsesRequestBaseUri()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("login.example.test:8443");
        context.Request.PathBase = "/identity";
        FrontChannelLogoutService service = this.CreateService(issuer: null, context);

        IReadOnlyList<FrontChannelLogoutFrame> frames = service.GenerateLogoutFrames(
            SessionId.Create(),
            [new ClientSessionInfo("client-1", "https://client.example.test/logout", null)]);

        frames.ShouldHaveSingleItem();
        frames[0].IframeUrl.ShouldContain("iss=https%3A%2F%2Flogin.example.test%3A8443%2Fidentity%2F");
    }

    [Fact]
    public void GenerateLogoutFrames_WithoutConfiguredIssuerOrRequest_Throws()
    {
        FrontChannelLogoutService service = this.CreateService(issuer: null);

        Should.Throw<InvalidOperationException>(() => service.GenerateLogoutFrames(
            SessionId.Create(),
            [new ClientSessionInfo("client-1", "https://client.example.test/logout", null)]));
    }

    [Fact]
    public void GenerateLogoutFrames_UriWithExistingQueryParams_UsesAmpersand()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client-1", "https://client1.com/logout?existing=param", null)
        };

        // Act
        IReadOnlyList<FrontChannelLogoutFrame> frames = this._service.GenerateLogoutFrames(sessionId, clients);

        // Assert
        frames.ShouldHaveSingleItem();
        frames[0].IframeUrl.ShouldContain("&sid=");
    }

    [Fact]
    public void GenerateLogoutFrames_UriWithoutQueryParams_UsesQuestionMark()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client-1", "https://client1.com/logout", null)
        };

        // Act
        IReadOnlyList<FrontChannelLogoutFrame> frames = this._service.GenerateLogoutFrames(sessionId, clients);

        // Assert
        frames.ShouldHaveSingleItem();
        frames[0].IframeUrl.ShouldContain("?sid=");
    }

    [Fact]
    public void GenerateLogoutFrames_MultipleClients_GeneratesFramesForAll()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client-1", "https://client1.com/logout", null),
            new("client-2", "https://client2.com/signout", null),
            new("client-3", null, "https://client3.com/backchannel") // No front-channel
        };

        // Act
        IReadOnlyList<FrontChannelLogoutFrame> frames = this._service.GenerateLogoutFrames(sessionId, clients);

        // Assert
        frames.Count.ShouldBe(2);
        frames.ShouldContain(f => f.ClientId == "client-1");
        frames.ShouldContain(f => f.ClientId == "client-2");
        frames.ShouldNotContain(f => f.ClientId == "client-3");
    }

    [Fact]
    public void GenerateLogoutFrames_EmptyClientList_ReturnsEmpty()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>();

        // Act
        IReadOnlyList<FrontChannelLogoutFrame> frames = this._service.GenerateLogoutFrames(sessionId, clients);

        // Assert
        frames.ShouldBeEmpty();
    }

    [Fact]
    public void GenerateLogoutFrames_AllClientsWithoutFrontChannel_ReturnsEmpty()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("client-1", null, "https://client1.com/backchannel"),
            new("client-2", null, "https://client2.com/backchannel")
        };

        // Act
        IReadOnlyList<FrontChannelLogoutFrame> frames = this._service.GenerateLogoutFrames(sessionId, clients);

        // Assert
        frames.ShouldBeEmpty();
    }

    [Fact]
    public void GenerateLogoutFrames_PreservesClientOrder()
    {
        // Arrange
        var sessionId = SessionId.Create();
        var clients = new List<ClientSessionInfo>
        {
            new("first-client", "https://first.com/logout", null),
            new("second-client", "https://second.com/logout", null),
            new("third-client", "https://third.com/logout", null)
        };

        // Act
        IReadOnlyList<FrontChannelLogoutFrame> frames = this._service.GenerateLogoutFrames(sessionId, clients);

        // Assert
        frames.Count.ShouldBe(3);
        frames[0].ClientId.ShouldBe("first-client");
        frames[1].ClientId.ShouldBe("second-client");
        frames[2].ClientId.ShouldBe("third-client");
    }

    private FrontChannelLogoutService CreateService(string? issuer, HttpContext? context = null)
    {
        return new FrontChannelLogoutService(
            this._logger,
            new HttpContextAccessor { HttpContext = context },
            issuer);
    }
}
