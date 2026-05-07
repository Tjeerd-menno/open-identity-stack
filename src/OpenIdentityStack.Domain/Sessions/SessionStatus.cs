namespace OpenIdentityStack.Domain.Sessions;

/// <summary>
/// Represents the status of a user session.
/// </summary>
public enum SessionStatus
{
    /// <summary>
    /// The session is active and valid.
    /// </summary>
    Active = 0,

    /// <summary>
    /// The session has been explicitly revoked by user or administrator.
    /// </summary>
    Revoked = 1,

    /// <summary>
    /// The session has expired due to timeout.
    /// </summary>
    Expired = 2,

    /// <summary>
    /// The session ended via normal logout.
    /// </summary>
    LoggedOut = 3,
}
