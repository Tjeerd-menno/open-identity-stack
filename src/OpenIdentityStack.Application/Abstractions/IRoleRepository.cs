using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Abstractions;
/// <summary>
/// Repository interface for role operations.
/// </summary>
public interface IRoleRepository
{
    /// <summary>
    /// Gets a role by its ID.
    /// </summary>
    /// <param name="id">The role ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The role if found, otherwise null.</returns>
    Task<Role?> GetByIdAsync(RoleId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a role by its normalized name.
    /// </summary>
    /// <param name="name">The normalized role name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The role if found, otherwise null.</returns>
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all roles with optional filtering.
    /// </summary>
    /// <param name="includeInactive">Whether to include inactive roles.</param>
    /// <param name="skip">Number of records to skip.</param>
    /// <param name="take">Number of records to take.</param>
    /// <param name="search">Optional search term to filter by name or display name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of roles.</returns>
    Task<IReadOnlyList<Role>> GetAllAsync(
        bool includeInactive = false,
        int skip = 0,
        int take = 100,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of roles.
    /// </summary>
    /// <param name="includeInactive">Whether to include inactive roles.</param>
    /// <param name="search">Optional search term to filter by name or display name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total count.</returns>
    Task<int> GetCountAsync(bool includeInactive = false, string? search = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new role.
    /// </summary>
    /// <param name="role">The role to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(Role role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a role from the repository.
    /// </summary>
    /// <param name="role">The role to remove.</param>
    void Remove(Role role);

    /// <summary>
    /// Checks if a role name already exists.
    /// </summary>
    /// <param name="name">The normalized role name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the name exists.</returns>
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a role to a user.
    /// </summary>
    /// <param name="assignment">The role assignment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AssignRoleAsync(RoleAssignment assignment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a role assignment from a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="roleId">The role ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveRoleAssignmentAsync(UserId userId, RoleId roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a role is assigned to a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="roleId">The role ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the role is assigned.</returns>
    Task<bool> IsRoleAssignedAsync(UserId userId, RoleId roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all roles assigned to a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="activeOnly">Whether to return only active roles.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of role assignments with their roles.</returns>
    Task<IReadOnlyList<Role>> GetUserRolesAsync(
        UserId userId,
        bool activeOnly = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all pending changes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
