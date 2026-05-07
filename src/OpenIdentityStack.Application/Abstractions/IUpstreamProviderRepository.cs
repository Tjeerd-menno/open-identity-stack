using OpenIdentityStack.Domain.Federation;

namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Repository for upstream provider operations.
/// </summary>
public interface IUpstreamProviderRepository
{
    /// <summary>
    /// Gets an upstream provider by ID.
    /// </summary>
    Task<UpstreamProvider?> GetByIdAsync(UpstreamProviderId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an upstream provider by name.
    /// </summary>
    Task<UpstreamProvider?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active upstream providers.
    /// </summary>
    Task<IReadOnlyList<UpstreamProvider>> GetActiveProvidersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all upstream providers.
    /// </summary>
    Task<IReadOnlyList<UpstreamProvider>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new upstream provider.
    /// </summary>
    Task AddAsync(UpstreamProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves changes to the repository.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
