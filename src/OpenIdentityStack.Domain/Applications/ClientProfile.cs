namespace OpenIdentityStack.Domain.Applications;

public enum ClientProfile
{
    Public = 1,
    Confidential = 2
}

internal static class ClientProfileExtensions
{
    public static OAuthClientType ToOAuthClientType(this ClientProfile profile) =>
        profile == ClientProfile.Confidential
            ? OAuthClientType.Confidential
            : OAuthClientType.Public;

    public static ClientProfile ToClientProfile(this OAuthClientType clientType) =>
        clientType == OAuthClientType.Confidential
            ? ClientProfile.Confidential
            : ClientProfile.Public;
}
