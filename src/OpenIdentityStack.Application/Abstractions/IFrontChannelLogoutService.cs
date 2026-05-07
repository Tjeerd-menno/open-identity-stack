namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Generates front-channel logout iframe URLs for session termination.
/// Front-channel logout uses browser iframes to notify clients.
/// </summary>
public interface IFrontChannelLogoutService
{
    /// <summary>
    /// Generates iframe URLs for front-channel logout to participating clients.
    /// </summary>
    /// <param name="sessionId">The session being terminated.</param>
    /// <param name="clients">The clients that support front-channel logout.</param>
    /// <returns>A list of iframe URLs.</returns>
    IReadOnlyList<FrontChannelLogoutFrame> GenerateLogoutFrames(
        SessionId sessionId,
        IReadOnlyList<ClientSessionInfo> clients);
}

/// <summary>
/// Represents an iframe for front-channel logout.
/// </summary>
/// <param name="ClientId">The client being notified.</param>
/// <param name="IframeUrl">The URL to render in an iframe.</param>
public sealed record FrontChannelLogoutFrame(string ClientId, string IframeUrl);
