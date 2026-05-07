using OpenIdentityStack.Domain.Clients;

namespace OpenIdentityStack.Application.Clients.Queries;

/// <summary>
/// Query for listing clients with pagination.
/// </summary>
public sealed record ListClientsQuery(
    int Page = 1,
    int PageSize = 30);

/// <summary>
/// Details about a client for list views.
/// </summary>
public sealed record ClientListItem(
    ClientId Id,
    string ClientIdValue,
    string DisplayName,
    ClientType ClientType,
    IReadOnlyList<string> AllowedGrantTypes,
    DateTimeOffset CreatedAt);

/// <summary>
/// Response for listing clients.
/// </summary>
public sealed record ListClientsResult(
    IReadOnlyList<ClientListItem> Items,
    int TotalCount,
    int Page,
    int PageSize);
