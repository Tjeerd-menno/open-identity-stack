using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>
/// Creates signed logout tokens for OpenID Connect Back-Channel Logout notifications.
/// </summary>
public interface ILogoutTokenFactory
{
    /// <summary>
    /// Creates a signed logout token for the specified session and audience.
    /// </summary>
    /// <param name="sessionId">The session being terminated.</param>
    /// <param name="clientId">The client the token is addressed to (the <c>aud</c> claim).</param>
    /// <returns>A compact-serialized, asymmetrically signed JWT.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no asymmetric signing credentials or issuer can be resolved. Back-channel
    /// logout fails closed rather than emitting a token relying parties cannot verify.
    /// </exception>
    string CreateLogoutToken(SessionId sessionId, string clientId);
}
