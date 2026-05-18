using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Repository interface for User entities.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Gets a user by their unique identifier.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user if found, null otherwise.</returns>
    Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their email address.
    /// </summary>
    /// <param name="email">The email address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user if found, null otherwise.</returns>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their preferred username.
    /// </summary>
    /// <param name="preferredUsername">The preferred username.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user if found, null otherwise.</returns>
    Task<User?> GetByPreferredUsernameAsync(string preferredUsername, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user with the given email exists.
    /// </summary>
    /// <param name="email">The email address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a user with the email exists.</returns>
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new user to the repository.
    /// </summary>
    /// <param name="user">The user to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user.
    /// </summary>
    /// <param name="user">The user to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user from the repository.
    /// </summary>
    /// <param name="user">The user to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists users with pagination and optional filtering.
    /// </summary>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="search">Optional search term for email or display name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple of the items and total count.</returns>
    Task<(IReadOnlyList<User> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a user by their upstream identity.
    /// </summary>
    /// <param name="providerId">The upstream provider ID.</param>
    /// <param name="subjectId">The subject ID from the upstream provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user if found, null otherwise.</returns>
    Task<User?> FindByUpstreamIdentityAsync(
        UpstreamProviderId providerId,
        string subjectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all changes to the repository.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
