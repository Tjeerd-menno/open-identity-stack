using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Queries;

public class GetUserGroupsQueryHandler : IGetUserGroupsQueryHandler
{
    private readonly IGroupRepository repository;

    public GetUserGroupsQueryHandler(IGroupRepository repository)
    {
        this.repository = repository;
    }

    public async Task<Result<List<GroupResponse>>> HandleAsync(
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Group> groups = await this.repository.GetGroupsForUserAsync(userId, cancellationToken);

        var response = groups.Select(g => new GroupResponse(
            g.Id.Value,
            g.Name,
            g.Description,
            g.ParentGroupId?.Value,
            g.CreatedAt.UtcDateTime // Group.CreatedAt is DateTimeOffset? Response uses DateTime usually.
        )).ToList();

        return response;
    }
}
