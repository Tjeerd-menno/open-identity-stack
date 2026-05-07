using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Sessions;

/// <summary>
/// Represents a client application that is part of a user session.
/// When a user authenticates to a client via OpenIddict, this tracks
/// that the client is participating in the session for SLO purposes.
/// </summary>
public sealed class ClientSession : IEquatable<ClientSession>
{
    private ClientSession(
        string clientId,
        DateTimeOffset authenticatedAt)
    {
        this.ClientId = clientId;
        this.AuthenticatedAt = authenticatedAt;
        this.LastAccessAt = authenticatedAt;
    }

    // EF Core constructor
    private ClientSession()
    {
        this.ClientId = string.Empty;
    }

    /// <summary>
    /// Gets the client application ID (OpenIddict application ID).
    /// </summary>
    public string ClientId { get; private set; }

    /// <summary>
    /// Gets when the user first authenticated to this client in this session.
    /// </summary>
    public DateTimeOffset AuthenticatedAt { get; private set; }

    /// <summary>
    /// Gets when the user last accessed this client.
    /// </summary>
    public DateTimeOffset LastAccessAt { get; private set; }

    /// <summary>
    /// Gets the front-channel logout URI if the client supports it.
    /// </summary>
    public string? FrontChannelLogoutUri { get; private set; }

    /// <summary>
    /// Gets the back-channel logout URI if the client supports it.
    /// </summary>
    public string? BackChannelLogoutUri { get; private set; }

    /// <summary>
    /// Creates a new ClientSession.
    /// </summary>
    public static Result<ClientSession> Create(
        string clientId,
        IDateTimeProvider dateTimeProvider)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return SessionErrors.ClientIdRequired;
        }

        return new ClientSession(clientId, dateTimeProvider.UtcNow);
    }

    /// <summary>
    /// Updates the last access time for this client.
    /// </summary>
    public void UpdateLastAccess(IDateTimeProvider dateTimeProvider)
    {
        this.LastAccessAt = dateTimeProvider.UtcNow;
    }

    /// <summary>
    /// Sets the logout URIs for this client.
    /// </summary>
    public void SetLogoutUris(string? frontChannelUri, string? backChannelUri)
    {
        this.FrontChannelLogoutUri = frontChannelUri;
        this.BackChannelLogoutUri = backChannelUri;
    }

    public bool Equals(ClientSession? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return this.ClientId == other.ClientId;
    }

    public override bool Equals(object? obj) => this.Equals(obj as ClientSession);

    public override int GetHashCode() => this.ClientId.GetHashCode();

    public static bool operator ==(ClientSession? left, ClientSession? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(ClientSession? left, ClientSession? right) =>
        !(left == right);
}
