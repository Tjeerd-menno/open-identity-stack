using SharedKernel;

namespace OpenIdentityStack.Domain.Applications;

/// <summary>
/// Defines domain errors for application operations.
/// </summary>
public static class ApplicationErrors
{
    public static readonly DomainError ClientIdRequired =
        DomainError.Validation("Application.ClientIdRequired", "Client ID is required.");

    public static readonly DomainError ClientIdTooLong =
        DomainError.Validation("Application.ClientIdTooLong", "Client ID must be 255 characters or less.");

    public static readonly DomainError DisplayNameRequired =
        DomainError.Validation("Application.DisplayNameRequired", "Display name is required.");

    public static readonly DomainError DisplayNameTooLong =
        DomainError.Validation("Application.DisplayNameTooLong", "Display name must be 255 characters or less.");

    public static readonly DomainError DescriptionTooLong =
        DomainError.Validation("Application.DescriptionTooLong", "Description must be 1000 characters or less.");

    public static readonly DomainError GrantTypesRequired =
        DomainError.Validation("Application.GrantTypesRequired", "At least one grant type is required.");

    public static readonly DomainError InvalidGrantType =
        DomainError.Validation("Application.InvalidGrantType", "Grant type is not valid for this application.");

    public static readonly DomainError InvalidScope =
        DomainError.Validation("Application.InvalidScope", "Scope cannot be null or whitespace.");

    public static readonly DomainError InvalidRedirectUri =
        DomainError.Validation("Application.InvalidRedirectUri", "Redirect URI must be a valid absolute URI.");

    public static readonly DomainError RedirectUriRequired =
        DomainError.Validation("Application.RedirectUriRequired", "At least one redirect URI is required for authorization code flow.");

    public static readonly DomainError PkceRequired =
        DomainError.Validation("Application.PkceRequired", "PKCE is required for public authorization code applications.");

    public static readonly DomainError ConfidentialClientRequired =
        DomainError.Validation("Application.ConfidentialClientRequired", "This application profile requires a confidential client.");

    public static readonly DomainError RedirectUrisNotAllowed =
        DomainError.Validation("Application.RedirectUrisNotAllowed", "Redirect URIs are not allowed for this application profile.");

    public static readonly DomainError CredentialsNotAllowedForPublic =
        DomainError.Validation("Application.CredentialsNotAllowedForPublic", "Credentials are not allowed for public applications.");

    public static readonly DomainError SecretHashRequired =
        DomainError.Validation("Application.SecretHashRequired", "Secret hash is required.");

    public static readonly DomainError CertificateThumbprintRequired =
        DomainError.Validation("Application.CertificateThumbprintRequired", "Certificate thumbprint is required.");

    public static readonly DomainError CredentialNotFound =
        DomainError.NotFound("Application.CredentialNotFound", "Application credential not found.");

    public static readonly DomainError CredentialAlreadyRevoked =
        DomainError.Validation("Application.CredentialAlreadyRevoked", "Application credential is already revoked.");

    public static readonly DomainError CredentialNotActive =
        DomainError.Validation("Application.CredentialNotActive", "Application credential is not active.");

    public static readonly DomainError InvalidCredentials =
        DomainError.Unauthorized("Application.InvalidCredentials", "Application credentials are invalid.");

    public static readonly DomainError Disabled =
        DomainError.Forbidden("Application.Disabled", "Application is disabled.");

    public static readonly DomainError AlreadyDisabled =
        DomainError.Validation("Application.AlreadyDisabled", "The application is already disabled.");

    public static readonly DomainError AlreadyActive =
        DomainError.Validation("Application.AlreadyActive", "The application is already active.");

    public static readonly DomainError NotFound =
        DomainError.NotFound("Application.NotFound", "Application not found.");

    public static readonly DomainError ClientIdExists =
        DomainError.Conflict("Application.ClientIdExists", "An application with this client ID already exists.");
}
