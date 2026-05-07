using OpenIdentityStack.Domain.Clients;

namespace OpenIdentityStack.Application.Clients.Commands;

/// <summary>
/// Command to create a new OAuth2/OIDC client.
/// </summary>
public sealed record CreateClientCommand(
    string ClientId,
    string DisplayName,
    ClientType ClientType,
    string? Description,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> AllowedGrantTypes,
    bool RequirePkce,
    bool RequireConsent);

/// <summary>
/// Response from creating a new client.
/// </summary>
public sealed record CreateClientResult(
    ClientId ClientId,
    string ClientIdValue,
    string DisplayName,
    string? ClientSecret);
