using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Queries;

/// <summary>
/// Handler interface for listing group mappings.
/// </summary>
public interface IListGroupMappingsQueryHandler
{
    /// <summary>
    /// Lists mappings for a group.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the list of group mapping responses.</returns>
    Task<Result<List<GroupMappingResponse>>> HandleAsync(
        GroupId groupId,
        CancellationToken cancellationToken = default);
}
