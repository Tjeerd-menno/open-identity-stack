namespace OpenIdentityStack.Domain.Groups;

/// <summary>
/// Specifies the target token for a mapping rule.
/// </summary>
public enum TokenTarget
{
    /// <summary>
    /// Add to Access Token only.
    /// </summary>
    AccessToken = 1,

    /// <summary>
    /// Add to ID Token only.
    /// </summary>
    IdToken = 2,

    /// <summary>
    /// Add to both Access and ID Tokens.
    /// </summary>
    Both = 3
}
