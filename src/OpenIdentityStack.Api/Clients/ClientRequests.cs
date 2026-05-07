using OpenIdentityStack.Domain.Clients;

namespace OpenIdentityStack.Api.Clients;

/// <summary>
/// Request to create a new OAuth2/OIDC client.
/// </summary>
public sealed record CreateClientRequest(
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
/// Request to update a client's basic information.
/// </summary>
public sealed record UpdateClientRequest(
    string DisplayName,
    string? Description);

/// <summary>
/// Response after creating a new client.
/// </summary>
public sealed record ClientCreatedResponse(
    Guid Id,
    string ClientId,
    string DisplayName,
    string? ClientSecret,
    DateTimeOffset CreatedAt);

/// <summary>
/// Response for a client.
/// </summary>
public sealed record ClientResponse(
    Guid Id,
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

/// <summary>
/// Response for a client list item.
/// </summary>
public sealed record ClientListItemResponse(
    Guid Id,
    string ClientIdValue,
    string DisplayName,
    ClientType ClientType,
    IReadOnlyList<string> AllowedGrantTypes,
    DateTimeOffset CreatedAt);

/// <summary>
/// Response for listing clients with pagination.
/// </summary>
public sealed record ListClientsResponse(
    IReadOnlyList<ClientListItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
