using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Commands;

public sealed record UpdateGroupCommand(GroupId Id, string? Name, string? Description);

public interface IUpdateGroupUseCase
{
    Task<Result> ExecuteAsync(UpdateGroupCommand command, CancellationToken cancellationToken = default);
}

public sealed class UpdateGroupUseCase : IUpdateGroupUseCase
{
    private readonly IGroupRepository repository;
    private readonly IDateTimeProvider dateTimeProvider;

    public UpdateGroupUseCase(IGroupRepository repository, IDateTimeProvider dateTimeProvider)
    {
        this.repository = repository;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> ExecuteAsync(UpdateGroupCommand command, CancellationToken cancellationToken = default)
    {
        Group? group = await this.repository.GetByIdAsync(command.Id, cancellationToken);
        if (group is null)
        {
            return new DomainError("Group.NotFound", "Group not found");
        }

        // Determine the name to use (keep current if not provided)
        string nameToUse = !string.IsNullOrWhiteSpace(command.Name) ? command.Name : group.Name;

        // Check uniqueness if name changed
        if (nameToUse != group.Name && await this.repository.ExistsByNameAsync(nameToUse, cancellationToken).ConfigureAwait(false))
        {
            return new DomainError("Group.NameExists", "Group name already exists");
        }

        // Use the Update method which returns Result and updates both name and description
        Result updateResult = group.Update(nameToUse, command.Description, this.dateTimeProvider);
        if (updateResult.IsFailure)
        {
            return updateResult.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
