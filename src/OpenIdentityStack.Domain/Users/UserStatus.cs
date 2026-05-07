namespace OpenIdentityStack.Domain.Users;

/// <summary>
/// Represents the status of a user account.
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// User has registered but not yet verified their email.
    /// </summary>
    PendingVerification = 0,

    /// <summary>
    /// User account is active and can authenticate.
    /// </summary>
    Active = 1,

    /// <summary>
    /// User account has been disabled by an administrator.
    /// </summary>
    Disabled = 2
}
