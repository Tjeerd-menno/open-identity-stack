using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Queries;

public class ListGroupsQueryHandler : IListGroupsQueryHandler
{
    private readonly IGroupRepository repository;

    public ListGroupsQueryHandler(IGroupRepository repository)
    {
        this.repository = repository;
    }

    public async Task<Result<GroupListResponse>> HandleAsync(
        int page = 1,
        int pageSize = 20,
            string? search = null,
            CancellationToken cancellationToken = default)
        {
            (IReadOnlyList<Group>? groups, int total) = await this.repository.ListAsync(page, pageSize, search, cancellationToken);

            var response = new GroupListResponse(
            groups.Select(g => new GroupResponse(
                g.Id.Value,
                g.Name,
                g.Description,
                g.ParentGroupId?.Value,
                g.CreatedAt.UtcDateTime)).ToList(),
            null // Pagination token logic can be added here if needed, or simplified
        );

        return response;
    }
}
