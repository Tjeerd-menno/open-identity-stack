using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Groups;

namespace OpenIdentityStack.Application.Groups.Queries;

public class GetGroupQueryHandler : IGetGroupQueryHandler
{
    private readonly IGroupRepository repository;

    public GetGroupQueryHandler(IGroupRepository repository)
    {
        this.repository = repository;
    }

    public async Task<Group?> HandleAsync(GroupId id, CancellationToken cancellationToken = default)
    {
        return await this.repository.GetByIdAsync(id, cancellationToken);
    }
}
