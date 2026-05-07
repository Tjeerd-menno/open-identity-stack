using OpenIdentityStack.Domain.ServiceAccounts;

namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Repository interface for ServiceAccount aggregate persistence.
/// </summary>
public interface IServiceAccountRepository
{
    /// <summary>
    /// Gets a service account by its ID.
    /// </summary>
    Task<ServiceAccount?> GetByIdAsync(ServiceAccountId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a service account by its OAuth2 client_id.
    /// </summary>
    Task<ServiceAccount?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a service account with the given client_id already exists.
    /// </summary>
    Task<bool> ExistsByClientIdAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new service account to the repository.
    /// </summary>
    Task AddAsync(ServiceAccount serviceAccount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists service accounts with pagination.
    /// </summary>
    Task<(List<ServiceAccount> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        ServiceAccountStatus? status,
        string? searchTerm,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing service account.
    /// </summary>
    void Update(ServiceAccount serviceAccount);

    /// <summary>
    /// Removes a service account from the repository.
    /// </summary>
    void Remove(ServiceAccount serviceAccount);

    /// <summary>
    /// Saves all changes to the repository.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
