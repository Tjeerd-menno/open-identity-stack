using OpenIdentityStack.Domain.Clients;

namespace OpenIdentityStack.Application.Clients.Queries;

/// <summary>
/// Details about a specific client.
/// </summary>
public sealed record ClientDetails(
    ClientId Id,
    string ClientIdValue,
    string DisplayName,
    ClientType ClientType,
    string? Description,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> AllowedGrantTypes,
    bool RequirePkce,
    bool RequireConsent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);
