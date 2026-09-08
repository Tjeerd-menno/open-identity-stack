using Microsoft.AspNetCore.Http;
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
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly string? configuredIssuer;

    public FrontChannelLogoutService(
        ILogger<FrontChannelLogoutService> logger,
        IHttpContextAccessor httpContextAccessor,
        string? configuredIssuer)
    {
        this.logger = logger;
        this.httpContextAccessor = httpContextAccessor;
        this.configuredIssuer = configuredIssuer;
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
        string issuer = this.ResolveIssuer();
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
            string logoutParameters = $"sid={Uri.EscapeDataString(sessionId.Value.ToString())}&iss={Uri.EscapeDataString(issuer)}";
            uriBuilder.Query = string.IsNullOrEmpty(existingQuery)
                ? logoutParameters
                : $"{existingQuery}&{logoutParameters}";
            string logoutUrl = uriBuilder.Uri.AbsoluteUri;

            frames.Add(new FrontChannelLogoutFrame(client.ClientId, logoutUrl));

            this.LogFrontChannelFrameGenerated(client.ClientId);
        }

        return frames;
    }

    private string ResolveIssuer()
    {
        if (!string.IsNullOrWhiteSpace(this.configuredIssuer))
        {
            return this.configuredIssuer;
        }

        HttpRequest? request = this.httpContextAccessor.HttpContext?.Request;
        if (request is not null && request.Host.HasValue)
        {
            string pathBase = request.PathBase.ToString();
            if (!pathBase.EndsWith('/'))
            {
                pathBase += "/";
            }

            return new UriBuilder(request.Scheme, request.Host.Host)
            {
                Port = request.Host.Port ?? -1,
                Path = pathBase,
            }.Uri.AbsoluteUri;
        }

        throw new InvalidOperationException(
            "The issuer could not be resolved. Configure 'OpenIddict:Issuer' so front-channel logout "
            + "frames carry an issuer clients can validate.");
    }
}
