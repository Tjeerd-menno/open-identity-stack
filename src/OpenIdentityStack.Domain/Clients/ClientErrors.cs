using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Clients;

/// <summary>
/// Defines domain errors for Client operations.
/// </summary>
public static class ClientErrors
{
    public static readonly DomainError ClientIdRequired =
        DomainError.Validation("Client.ClientIdRequired", "Client ID is required.");

    public static readonly DomainError ClientIdTooLong =
        DomainError.Validation("Client.ClientIdTooLong", "Client ID must be 255 characters or less.");

    public static readonly DomainError DisplayNameRequired =
        DomainError.Validation("Client.DisplayNameRequired", "Display name is required.");

    public static readonly DomainError DisplayNameTooLong =
        DomainError.Validation("Client.DisplayNameTooLong", "Display name must be 255 characters or less.");

    public static readonly DomainError NotFound =
        DomainError.NotFound("Client.NotFound", "Client not found.");

    public static readonly DomainError ClientIdExists =
        DomainError.Conflict("Client.ClientIdExists", "A client with this client ID already exists.");

    public static readonly DomainError InvalidRedirectUri =
        DomainError.Validation("Client.InvalidRedirectUri", "Redirect URI must be a valid absolute URI.");

    public static readonly DomainError RedirectUriRequired =
        DomainError.Validation("Client.RedirectUriRequired", "At least one redirect URI is required for authorization code flow.");

    public static readonly DomainError InvalidScope =
        DomainError.Validation("Client.InvalidScope", "Scope cannot be null or whitespace.");

    public static readonly DomainError InvalidGrantType =
        DomainError.Validation("Client.InvalidGrantType", "Grant type cannot be null or whitespace.");

    public static readonly DomainError SecretRequiredForConfidential =
        DomainError.Validation("Client.SecretRequiredForConfidential", "Client secret is required for confidential clients.");

    public static readonly DomainError SecretNotAllowedForPublic =
        DomainError.Validation("Client.SecretNotAllowedForPublic", "Client secret is not allowed for public clients.");
}
