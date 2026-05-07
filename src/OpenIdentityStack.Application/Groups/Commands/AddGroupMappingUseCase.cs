using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Commands;

public interface IAddGroupMappingUseCase
{
    Task<Result> ExecuteAsync(AddGroupMappingCommand command, CancellationToken cancellationToken = default);
}

public sealed class AddGroupMappingUseCase : IAddGroupMappingUseCase
{
    private readonly IGroupRepository groupRepository;
    private readonly IDateTimeProvider dateTimeProvider;

    public AddGroupMappingUseCase(
        IGroupRepository groupRepository,
        IDateTimeProvider dateTimeProvider)
    {
        this.groupRepository = groupRepository;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> ExecuteAsync(AddGroupMappingCommand command, CancellationToken cancellationToken = default)
    {
        Group? group = await this.groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return new DomainError("Group.NotFound", "Group not found.");
        }

        try
        {
            group.AddMapping(
                command.Type,
                command.Target,
                command.Value,
                command.TokenTarget,
                this.dateTimeProvider);
        }
        catch (ArgumentException ex)
        {
             return DomainError.Validation("GroupMapping.Invalid", ex.Message);
        }

        await this.groupRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
