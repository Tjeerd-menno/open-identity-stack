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
    private readonly string issuer;

    public FrontChannelLogoutService(ILogger<FrontChannelLogoutService> logger)
        : this(logger, "open-identity-stack")
    {
    }

    public FrontChannelLogoutService(ILogger<FrontChannelLogoutService> logger, string issuer)
    {
        this.logger = logger;
        this.issuer = issuer;
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

            if (!Uri.TryCreate(client.FrontChannelLogoutUri, UriKind.Absolute, out Uri? logoutUri) ||
                (logoutUri.Scheme != Uri.UriSchemeHttp && logoutUri.Scheme != Uri.UriSchemeHttps))
            {
                this.LogNoFrontChannelLogout(client.ClientId);
                continue;
            }

            var uriBuilder = new UriBuilder(logoutUri);
            string existingQuery = uriBuilder.Query.TrimStart('?');
            string logoutParameters = $"sid={Uri.EscapeDataString(sessionId.Value.ToString())}&iss={Uri.EscapeDataString(this.issuer)}";
            uriBuilder.Query = string.IsNullOrEmpty(existingQuery)
                ? logoutParameters
                : $"{existingQuery}&{logoutParameters}";
            string logoutUrl = uriBuilder.Uri.AbsoluteUri;

            frames.Add(new FrontChannelLogoutFrame(client.ClientId, logoutUrl));

            this.LogFrontChannelFrameGenerated(client.ClientId);
        }

        return frames;
    }
}
