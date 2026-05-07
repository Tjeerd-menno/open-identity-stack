using SharedKernel;
namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Interface for checking user permissions.
/// </summary>
public interface IPermissionChecker
{
    /// <summary>
    /// Checks if a user has a specific permission.
    /// </summary>
    /// <param name="userId">The user ID to check.</param>
    /// <param name="permission">The permission to check for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user has the permission.</returns>
    Task<bool> HasPermissionAsync(
        UserId userId,
        string permission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has any of the specified permissions.
    /// </summary>
    /// <param name="userId">The user ID to check.</param>
    /// <param name="permissions">The permissions to check for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user has any of the permissions.</returns>
    Task<bool> HasAnyPermissionAsync(
        UserId userId,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has all of the specified permissions.
    /// </summary>
    /// <param name="userId">The user ID to check.</param>
    /// <param name="permissions">The permissions to check for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user has all of the permissions.</returns>
    Task<bool> HasAllPermissionsAsync(
        UserId userId,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all effective permissions for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list of all permissions the user has.</returns>
    Task<IReadOnlyList<string>> GetAllPermissionsAsync(
        UserId userId,
        CancellationToken cancellationToken = default);
}
