using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>
/// Generates front-channel logout iframe URLs for session termination.
/// Front-channel logout uses browser iframes to notify clients.
/// </summary>
public sealed partial class FrontChannelLogoutService : IFrontChannelLogoutService
{
    private readonly ILogger<FrontChannelLogoutService> logger;

    public FrontChannelLogoutService(ILogger<FrontChannelLogoutService> logger)
    {
        this.logger = logger;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client {ClientId} does not have front-channel logout configured")]
    private partial void LogNoFrontChannelLogout(string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated front-channel logout frame for client {ClientId}")]
    private partial void LogFrontChannelFrameGenerated(string clientId);

    /// <inheritdoc />
    public IReadOnlyList<FrontChannelLogoutFrame> GenerateLogoutFrames(
        SessionId sessionId,
        IReadOnlyList<ClientSessionInfo> clients)
    {
        var frames = new List<FrontChannelLogoutFrame>();

        foreach (ClientSessionInfo client in clients)
        {
            if (string.IsNullOrEmpty(client.FrontChannelLogoutUri))
            {
                this.LogNoFrontChannelLogout(client.ClientId);
                continue;
            }

            // Build the logout URL with session identifier
            string separator = client.FrontChannelLogoutUri.Contains('?') ? "&" : "?";
            string logoutUrl = $"{client.FrontChannelLogoutUri}{separator}sid={sessionId.Value}&iss={Uri.EscapeDataString("open-identity-stack")}";

            frames.Add(new FrontChannelLogoutFrame(client.ClientId, logoutUrl));

            this.LogFrontChannelFrameGenerated(client.ClientId);
        }

        return frames;
    }
}
