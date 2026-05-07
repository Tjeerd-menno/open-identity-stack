
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
        this._service = new FrontChannelLogoutService(this._logger);
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
}
