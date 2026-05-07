using OpenIdentityStack.Domain.Settings;

namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Repository for authentication settings operations.
/// </summary>
public interface IAuthenticationSettingsRepository
{
    /// <summary>
    /// Gets the current authentication settings.
    /// If no settings exist, returns null.
    /// </summary>
    Task<AuthenticationSettings?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or creates the authentication settings.
    /// If no settings exist, creates default settings.
    /// </summary>
    Task<AuthenticationSettings> GetOrCreateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds new authentication settings.
    /// </summary>
    Task AddAsync(AuthenticationSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves changes to the repository.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
