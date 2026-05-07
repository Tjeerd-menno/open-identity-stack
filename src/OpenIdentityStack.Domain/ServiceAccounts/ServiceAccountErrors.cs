using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.ServiceAccounts;

/// <summary>
/// Error definitions for service account operations.
/// </summary>
public static class ServiceAccountErrors
{
    public static DomainError ClientIdRequired => new(
        "ServiceAccount.ClientIdRequired",
        "Client ID is required.");

    public static DomainError ClientIdTooLong => new(
        "ServiceAccount.ClientIdTooLong",
        "Client ID must not exceed 128 characters.");

    public static DomainError DisplayNameRequired => new(
        "ServiceAccount.DisplayNameRequired",
        "Display name is required.");

    public static DomainError DisplayNameTooLong => new(
        "ServiceAccount.DisplayNameTooLong",
        "Display name must not exceed 256 characters.");

    public static DomainError ScopesRequired => new(
        "ServiceAccount.ScopesRequired",
        "At least one allowed scope is required.");

    public static DomainError GrantTypesRequired => new(
        "ServiceAccount.GrantTypesRequired",
        "At least one allowed grant type is required.");

    public static DomainError InvalidGrantType(string grantType) => new(
        "ServiceAccount.InvalidGrantType",
        $"Grant type '{grantType}' is not valid. Valid types are: client_credentials, authorization_code, refresh_token, urn:ietf:params:oauth:grant-type:device_code.");

    public static DomainError AlreadyDisabled => new(
        "ServiceAccount.AlreadyDisabled",
        "The service account is already disabled.");

    public static DomainError AlreadyActive => new(
        "ServiceAccount.AlreadyActive",
        "The service account is already active.");

    public static DomainError CredentialNotFound => new(
        "ServiceAccount.CredentialNotFound",
        "The specified credential was not found.");

    public static DomainError CertificateNotFound => new(
        "ServiceAccount.CertificateNotFound",
        "The specified certificate was not found.");

    public static DomainError CertificateThumbprintExists => new(
        "ServiceAccount.CertificateThumbprintExists",
        "A certificate with this thumbprint already exists.");

    public static DomainError NotFound => new(
        "ServiceAccount.NotFound",
        "The service account was not found.");

    public static DomainError ClientIdExists => new(
        "ServiceAccount.ClientIdExists",
        "A service account with this client ID already exists.");

    public static DomainError ClientIdAlreadyExists(string clientId) => new(
        "ServiceAccount.ClientIdAlreadyExists",
        $"A service account with client ID '{clientId}' already exists.");

    public static DomainError Disabled => new(
        "ServiceAccount.Disabled",
        "The service account is disabled and cannot be modified.");

    public static DomainError NoValidCredentials => new(
        "ServiceAccount.NoValidCredentials",
        "The service account has no valid credentials.");

    public static DomainError InvalidCredentials => new(
        "ServiceAccount.InvalidCredentials",
        "The provided credentials are invalid.");

    public static DomainError AccountDisabled => new(
        "ServiceAccount.AccountDisabled",
        "The service account is disabled.");

    public static DomainError ClientRegistrationFailed => new(
        "ServiceAccount.ClientRegistrationFailed",
        "Failed to register the OAuth client in the authorization server.");
}
