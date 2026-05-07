using OpenIdentityStack.Domain.Groups;

namespace OpenIdentityStack.Application.Groups.Queries;

/// <summary>
/// Handler interface for getting a group by ID.
/// </summary>
public interface IGetGroupQueryHandler
{
    /// <summary>
    /// Gets a group by its ID.
    /// </summary>
    /// <param name="id">The group ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The group if found, null otherwise.</returns>
    Task<Group?> HandleAsync(GroupId id, CancellationToken cancellationToken = default);
}
