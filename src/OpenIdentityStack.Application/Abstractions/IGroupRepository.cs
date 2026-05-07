using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Repository interface for Group entities.
/// </summary>
public interface IGroupRepository
{
    /// <summary>
    /// Gets a group by its unique identifier.
    /// </summary>
    Task<Group?> GetByIdAsync(GroupId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a group by its name (case-sensitive or insensitive depends on impl, usually exact match unique).
    /// </summary>
    Task<Group?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all groups that a user is a member of.
    /// </summary>
    Task<IReadOnlyList<Group>> GetGroupsForUserAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists groups with pagination and optional search.
    /// </summary>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="search">Optional search term for group name.</param>
    /// <returns>A tuple containing the list of groups and the total count.</returns>
    Task<(IReadOnlyList<Group> Items, int TotalCount)> ListAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a group name already exists.
    /// </summary>
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new group to the repository.
    /// </summary>
    Task AddAsync(Group group, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a group from the repository.
    /// </summary>
    void Remove(Group group);

    /// <summary>
    /// Saves all changes to the repository.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
