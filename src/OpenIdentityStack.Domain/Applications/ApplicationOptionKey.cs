namespace OpenIdentityStack.Domain.Applications;

public enum ApplicationOptionKey
{
    ApplicationProfile = 0,
    ClientProfile = 1,
    TokenEndpointAuthenticationMethod = 2,
    ClientSecrets = 3,
    MultipleActiveSecrets = 4,
    SecretRotationAndRevocation = 5,
    ClientCertificates = 6,
    Jwks = 7,
    AllowedScopes = 8,
    AllowedAudiences = 9,
    AuthorizationCodeGrant = 10,
    DeviceCodeGrant = 11,
    ClientCredentialsGrant = 12,
    RefreshTokenGrant = 13,
    RedirectUris = 14,
    NativeRedirectUriPatterns = 15,
    LoopbackRedirectUri = 16,
    PostLogoutRedirectUris = 17,
    AllowedWebOrigins = 18,
    Pkce = 19,
    Consent = 20,
    LoginInitiationUri = 21,
    FrontChannelLogout = 22,
    BackChannelLogout = 23,
    DeviceUserCodeSettings = 24,
    DevicePollingAndExpiry = 25,
    TokenLifetimeOverrides = 26,
    SenderConstrainedAccessTokens = 27
}
