namespace OpenIdentityStack.Domain.Applications;

/// <summary>
/// Credential material type used by a confidential application.
/// </summary>
public enum ApplicationCredentialType
{
    ClientSecret = 0,
    X509Certificate = 1
}
