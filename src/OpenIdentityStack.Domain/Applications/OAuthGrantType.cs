namespace OpenIdentityStack.Domain.Applications;

/// <summary>
/// Supported OAuth grant profiles for application configuration.
/// </summary>
public enum OAuthGrantType
{
    AuthorizationCode = 0,
    ClientCredentials = 1,
    RefreshToken = 2,
    DeviceCode = 3
}
