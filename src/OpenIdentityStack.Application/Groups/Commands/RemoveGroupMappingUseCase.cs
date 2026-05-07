using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Commands;

public sealed record RemoveGroupMappingCommand(GroupId GroupId, Guid MappingId);

public interface IRemoveGroupMappingUseCase
{
    Task<Result> ExecuteAsync(RemoveGroupMappingCommand command, CancellationToken cancellationToken = default);
}

public sealed class RemoveGroupMappingUseCase : IRemoveGroupMappingUseCase
{
    private readonly IGroupRepository repository;
    private readonly IDateTimeProvider dateTimeProvider;

    public RemoveGroupMappingUseCase(IGroupRepository repository, IDateTimeProvider dateTimeProvider)
    {
        this.repository = repository;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> ExecuteAsync(RemoveGroupMappingCommand command, CancellationToken cancellationToken = default)
    {
        Group? group = await this.repository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return new DomainError("Group.NotFound", "Group not found");
        }

        // Strategy: We need to match the "MappingId" from the API to the Domain ValueObject.
        // Option 1: The API client knows how we construct IDs (MD5/SHA of props).
        // Option 2: The API client was given an ID in "Get" response, which we generated deterministically.
        // We need to replicate that generation here.

        GroupMapping? mappingToRemove = group.Mappings.FirstOrDefault(m => m.GetDeterministicId() == command.MappingId);
        
        if (mappingToRemove is null)
        {
            return new DomainError("GroupMapping.NotFound", "Mapping not found");
        }

        group.RemoveMapping(mappingToRemove, this.dateTimeProvider);
        await this.repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
