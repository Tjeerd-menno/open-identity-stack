using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Commands;

public interface IAddUserToGroupUseCase
{
    Task<Result> ExecuteAsync(AddUserToGroupCommand command, CancellationToken cancellationToken = default);
}

public sealed class AddUserToGroupUseCase : IAddUserToGroupUseCase
{
    private readonly IGroupRepository groupRepository;
    private readonly IUserRepository userRepository;
    private readonly IDateTimeProvider dateTimeProvider;

    public AddUserToGroupUseCase(
        IGroupRepository groupRepository,
        IUserRepository userRepository,
        IDateTimeProvider dateTimeProvider)
    {
        this.groupRepository = groupRepository;
        this.userRepository = userRepository;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> ExecuteAsync(AddUserToGroupCommand command, CancellationToken cancellationToken = default)
    {
        // Fetch entities sequentially to avoid DbContext concurrency issues
        Group? group = await this.groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        User? user = await this.userRepository.GetByIdAsync(command.UserId, cancellationToken);

        if (group is null)
        {
            return new DomainError("Group.NotFound", "Group not found.");
        }

        if (user is null)
        {
            return new DomainError("User.NotFound", "User not found.");
        }
        
        // Add member via aggregate
        group.AddMember(command.UserId, command.AssignedBy, this.dateTimeProvider);

        // Save
        // Assuming EF Core tracks changes to the loaded group entity
        await this.groupRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
