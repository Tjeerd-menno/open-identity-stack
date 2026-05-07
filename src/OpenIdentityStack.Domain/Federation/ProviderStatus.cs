namespace OpenIdentityStack.Domain.Federation;

/// <summary>
/// Represents the status of an upstream identity provider.
/// </summary>
public enum ProviderStatus
{
    /// <summary>
    /// The provider is active and can be used for authentication.
    /// </summary>
    Active = 0,

    /// <summary>
    /// The provider is disabled and cannot be used for authentication.
    /// </summary>
    Disabled = 1,
}
