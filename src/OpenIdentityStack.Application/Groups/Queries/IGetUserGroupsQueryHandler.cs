using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Queries;

/// <summary>
/// Handler interface for getting groups a user belongs to.
/// </summary>
public interface IGetUserGroupsQueryHandler
{
    /// <summary>
    /// Gets all groups a user belongs to.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the list of group responses.</returns>
    Task<Result<List<GroupResponse>>> HandleAsync(
        UserId userId,
        CancellationToken cancellationToken = default);
}
