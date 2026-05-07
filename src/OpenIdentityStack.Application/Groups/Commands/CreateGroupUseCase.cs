using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Commands;

/// <summary>
/// Use case interface for creating groups.
/// </summary>
public interface ICreateGroupUseCase
{
    /// <summary>
    /// Executes the create group command.
    /// </summary>
    /// <param name="command">The command containing group details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the response or an error.</returns>
    Task<Result<CreateGroupResponse>> ExecuteAsync(CreateGroupCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case for creating a new group.
/// </summary>
public sealed class CreateGroupUseCase : ICreateGroupUseCase
{
    private readonly IGroupRepository groupRepository;
    private readonly IDateTimeProvider dateTimeProvider;

    public CreateGroupUseCase(IGroupRepository groupRepository, IDateTimeProvider dateTimeProvider)
    {
        this.groupRepository = groupRepository;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<CreateGroupResponse>> ExecuteAsync(CreateGroupCommand command, CancellationToken cancellationToken = default)
    {
        // Check uniqueness
        if (await this.groupRepository.ExistsByNameAsync(command.Name, cancellationToken).ConfigureAwait(false))
        {
            return new DomainError("Group.NameAlreadyExists", $"A group with the name '{command.Name}' already exists.");
        }

        // Create domain entity
        Result<Group> groupResult = Group.Create(command.Name, command.Description, this.dateTimeProvider);
        if (groupResult.IsFailure)
        {
            return groupResult.Error;
        }

        Group group = groupResult.Value;

        // Persist
        await this.groupRepository.AddAsync(group, cancellationToken);
        await this.groupRepository.SaveChangesAsync(cancellationToken);

        return new CreateGroupResponse(group.Id.Value, group.Name, group.Description);
    }
}
