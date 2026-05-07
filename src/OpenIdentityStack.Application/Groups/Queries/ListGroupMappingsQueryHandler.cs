using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Queries;

public class ListGroupMappingsQueryHandler : IListGroupMappingsQueryHandler
{
    private readonly IGroupRepository repository;

    public ListGroupMappingsQueryHandler(IGroupRepository repository)
    {
        this.repository = repository;
    }

    public async Task<Result<List<GroupMappingResponse>>> HandleAsync(
        GroupId groupId,
        CancellationToken cancellationToken = default)
    {
        Group? group = await this.repository.GetByIdAsync(groupId, cancellationToken);
        if (group is null)
        {
            return new DomainError("Group.NotFound", "Group not found");
        }

        var response = group.Mappings.Select(m => new GroupMappingResponse(
             m.GetDeterministicId(),
             m.Type,
             m.Type == MappingType.Role && Guid.TryParse(m.Target, out Guid roleId) ? roleId : null,
             m.Type == MappingType.Claim ? m.Target : null,
             m.Value, 
             m.TokenTarget,
             0 
        )).ToList();

        return response;
    }
}
