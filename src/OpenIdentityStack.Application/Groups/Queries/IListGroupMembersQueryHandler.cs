using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Queries;

/// <summary>
/// Handler interface for listing group members.
/// </summary>
public interface IListGroupMembersQueryHandler
{
    /// <summary>
    /// Lists members of a group with pagination.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the user list response.</returns>
    Task<Result<UserListResponse>> HandleAsync(
        GroupId groupId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
