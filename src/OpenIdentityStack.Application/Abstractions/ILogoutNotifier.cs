using SharedKernel;
namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Information about a client session for logout notification.
/// </summary>
/// <param name="ClientId">The client application ID.</param>
/// <param name="FrontChannelLogoutUri">The front-channel logout URI (if supported).</param>
/// <param name="BackChannelLogoutUri">The back-channel logout URI (if supported).</param>
public sealed record ClientSessionInfo(
    string ClientId,
    string? FrontChannelLogoutUri,
    string? BackChannelLogoutUri);

/// <summary>
/// Result of sending logout notifications to clients.
/// </summary>
/// <param name="SuccessCount">Number of successful notifications.</param>
/// <param name="FailureCount">Number of failed notifications.</param>
/// <param name="FailedClients">List of client IDs that failed.</param>
public sealed record LogoutNotificationResult(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<string> FailedClients);

/// <summary>
/// Interface for sending logout notifications to participating clients.
/// Supports both front-channel (browser redirect) and back-channel (server-to-server) logout.
/// </summary>
public interface ILogoutNotifier
{
    /// <summary>
    /// Sends logout notifications to all specified clients.
    /// </summary>
    /// <param name="sessionId">The session being terminated.</param>
    /// <param name="clients">The clients to notify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the notification attempts.</returns>
    Task<LogoutNotificationResult> NotifyClientsAsync(
        SessionId sessionId,
        IReadOnlyList<ClientSessionInfo> clients,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates front-channel logout iframe URLs for the specified clients.
    /// </summary>
    /// <param name="sessionId">The session being terminated.</param>
    /// <param name="clients">The clients that support front-channel logout.</param>
    /// <returns>A list of iframe URLs for front-channel logout.</returns>
    IReadOnlyList<string> GenerateFrontChannelLogoutUrls(
        SessionId sessionId,
        IReadOnlyList<ClientSessionInfo> clients);
}
