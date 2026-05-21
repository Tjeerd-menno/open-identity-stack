using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Infrastructure.Serialization;

namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>
/// Implements back-channel logout notifications to clients.
/// Sends HTTP POST requests with logout tokens to registered back-channel logout URIs.
/// </summary>
public sealed partial class BackChannelLogoutNotifier : ILogoutNotifier
{
    private readonly HttpClient httpClient;
    private readonly ILogger<BackChannelLogoutNotifier> logger;

    public BackChannelLogoutNotifier(
        HttpClient httpClient,
        ILogger<BackChannelLogoutNotifier> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client {ClientId} does not have back-channel logout configured")]
    private partial void LogNoBackChannelLogout(string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully sent back-channel logout notification to {ClientId}")]
    private partial void LogBackChannelLogoutSuccess(string clientId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Back-channel logout notification to {ClientId} returned non-success status")]
    private partial void LogBackChannelLogoutNonSuccess(string clientId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send back-channel logout notification to {ClientId}")]
    private partial void LogBackChannelLogoutError(Exception ex, string clientId);

    public async Task<LogoutNotificationResult> NotifyClientsAsync(
        SessionId sessionId,
        IReadOnlyList<ClientSessionInfo> clients,
        CancellationToken cancellationToken = default)
    {
        int successCount = 0;
        var failedClients = new List<string>();

        foreach (ClientSessionInfo client in clients)
        {
            if (string.IsNullOrEmpty(client.BackChannelLogoutUri))
            {
                this.LogNoBackChannelLogout(client.ClientId);
                continue;
            }

            try
            {
                string logoutToken = CreateLogoutToken(sessionId, client.ClientId);
                bool success = await this.SendLogoutNotificationAsync(
                    client.BackChannelLogoutUri,
                    logoutToken,
                    cancellationToken);

                if (success)
                {
                    this.LogBackChannelLogoutSuccess(client.ClientId);
                    successCount++;
                }
                else
                {
                    this.LogBackChannelLogoutNonSuccess(client.ClientId);
                    failedClients.Add(client.ClientId);
                }
            }
            catch (Exception ex)
            {
                this.LogBackChannelLogoutError(ex, client.ClientId);
                failedClients.Add(client.ClientId);
            }
        }

        return new LogoutNotificationResult(successCount, failedClients.Count, failedClients);
    }

    public IReadOnlyList<string> GenerateFrontChannelLogoutUrls(
        SessionId sessionId,
        IReadOnlyList<ClientSessionInfo> clients)
    {
        var urls = new List<string>();

        foreach (ClientSessionInfo client in clients)
        {
            if (string.IsNullOrEmpty(client.FrontChannelLogoutUri))
            {
                continue;
            }

            // Add session identifier to front-channel logout URL
            string separator = client.FrontChannelLogoutUri.Contains('?') ? "&" : "?";
            string logoutUrl = $"{client.FrontChannelLogoutUri}{separator}sid={sessionId.Value}";
            urls.Add(logoutUrl);
        }

        return urls;
    }

    private static string CreateLogoutToken(SessionId sessionId, string clientId)
    {
        // Create a simple unsigned logout token for back-channel notification
        // In production, this should be signed with the server's key
        // and use proper JWT library from OpenIddict
        var payload = new BackChannelLogoutPayload(
            Issuer: "open-identity-stack", // TODO: Get from configuration
            Subject: sessionId.Value.ToString(),
            Audience: clientId,
            IssuedAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            JwtId: Guid.NewGuid().ToString(),
            SessionId: sessionId.Value.ToString(),
            Events: new Dictionary<string, BackChannelLogoutEvent>
            {
                ["http://schemas.openid.net/event/backchannel-logout"] = new()
            });

        string header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string payloadJson = JsonSerializer.Serialize(
            payload,
            InfrastructureJsonContext.Default.BackChannelLogoutPayload);
        string payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        // Unsigned JWT (alg: none) - for demo only
        // Production should use OpenIddict's token generation
        return $"{header}.{payloadBase64}.";
    }

    private async Task<bool> SendLogoutNotificationAsync(
        string logoutUri,
        string logoutToken,
        CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["logout_token"] = logoutToken
        });

        HttpResponseMessage response = await this.httpClient.PostAsync(logoutUri, content, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}

internal sealed record BackChannelLogoutPayload(
    [property: JsonPropertyName("iss")] string Issuer,
    [property: JsonPropertyName("sub")] string Subject,
    [property: JsonPropertyName("aud")] string Audience,
    [property: JsonPropertyName("iat")] long IssuedAt,
    [property: JsonPropertyName("jti")] string JwtId,
    [property: JsonPropertyName("sid")] string SessionId,
    [property: JsonPropertyName("events")] IReadOnlyDictionary<string, BackChannelLogoutEvent> Events);

internal sealed record BackChannelLogoutEvent;
