using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Commands;

public sealed record DeleteGroupCommand(GroupId Id);

public interface IDeleteGroupUseCase
{
    Task<Result> ExecuteAsync(DeleteGroupCommand command, CancellationToken cancellationToken = default);
}

public sealed class DeleteGroupUseCase : IDeleteGroupUseCase
{
    private readonly IGroupRepository repository;

    public DeleteGroupUseCase(IGroupRepository repository)
    {
        this.repository = repository;
    }

    public async Task<Result> ExecuteAsync(DeleteGroupCommand command, CancellationToken cancellationToken = default)
    {
        Group? group = await this.repository.GetByIdAsync(command.Id, cancellationToken);
        if (group is null)
        {
            return new DomainError("Group.NotFound", "Group not found");
        }

        // Logic check: Can we delete if it has members? Usually yes.
        // Can we delete if it has children? Usually restrict.
        if (group.ChildGroups.Count > 0)
        {
             return new DomainError("Group.HasChildren", "Cannot delete group with child groups.");
        }

        this.repository.Remove(group);
        await this.repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
