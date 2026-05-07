namespace OpenIdentityStack.Domain.ServiceAccounts;

/// <summary>
/// Represents the status of a service account.
/// </summary>
public enum ServiceAccountStatus
{
    /// <summary>
    /// The service account is active and can authenticate.
    /// </summary>
    Active = 1,

    /// <summary>
    /// The service account is disabled and cannot authenticate.
    /// </summary>
    Disabled = 2
}
