using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Commands;

public interface IRemoveUserFromGroupUseCase
{
    Task<Result> ExecuteAsync(RemoveUserFromGroupCommand command, CancellationToken cancellationToken = default);
}

public sealed class RemoveUserFromGroupUseCase : IRemoveUserFromGroupUseCase
{
    private readonly IGroupRepository groupRepository;
    private readonly IDateTimeProvider dateTimeProvider;

    public RemoveUserFromGroupUseCase(
        IGroupRepository groupRepository,
        IDateTimeProvider dateTimeProvider)
    {
        this.groupRepository = groupRepository;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> ExecuteAsync(RemoveUserFromGroupCommand command, CancellationToken cancellationToken = default)
    {
        Group? group = await this.groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return new DomainError("Group.NotFound", "Group not found.");
        }

        group.RemoveMember(command.UserId, this.dateTimeProvider);

        await this.groupRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
