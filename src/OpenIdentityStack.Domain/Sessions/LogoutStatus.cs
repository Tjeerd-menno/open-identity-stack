namespace OpenIdentityStack.Domain.Sessions;

/// <summary>
/// Represents the status of a logout operation for a client.
/// Used to track Single Logout (SLO) progress.
/// </summary>
public enum LogoutStatus
{
    /// <summary>
    /// Logout notification has not been sent yet.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Logout notification was sent and acknowledged.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// Logout notification failed.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Client does not support logout notifications.
    /// </summary>
    NotSupported = 3,
}
