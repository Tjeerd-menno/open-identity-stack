using OpenIdentityStack.Domain.Clients;

namespace OpenIdentityStack.Application.Clients;

/// <summary>
/// Repository interface for managing Client entities.
/// </summary>
public interface IClientRepository
{
    /// <summary>
    /// Gets a client by its ID.
    /// </summary>
    Task<Client?> GetByIdAsync(ClientId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a client by its client ID value.
    /// </summary>
    Task<Client?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all clients with pagination.
    /// </summary>
    Task<(IReadOnlyList<Client> Items, int TotalCount)> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new client to the repository.
    /// </summary>
    Task AddAsync(Client client, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a client from the repository.
    /// </summary>
    Task DeleteAsync(Client client, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all changes made to clients.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
