namespace OpenIdentityStack.Domain.Clients;

/// <summary>
/// Defines the type of OAuth2/OIDC client.
/// </summary>
public enum ClientType
{
    /// <summary>
    /// Confidential client - can securely maintain a client secret (e.g., web server apps, service accounts).
    /// </summary>
    Confidential,

    /// <summary>
    /// Public client - cannot securely maintain a client secret (e.g., SPAs, mobile apps).
    /// </summary>
    Public
}
