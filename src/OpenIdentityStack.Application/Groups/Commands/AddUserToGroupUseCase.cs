using OpenIdentityStack.Application.Authorization;
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
    private readonly IAdministrativeApproval approval;
    private readonly UnrestrictedGrantPolicy unrestrictedPolicy;
    private readonly IUserRepository userRepository;
    private readonly IDateTimeProvider dateTimeProvider;

    public AddUserToGroupUseCase(
        IGroupRepository groupRepository,
        IUserRepository userRepository,
        IDateTimeProvider dateTimeProvider,
        IAdministrativeApproval approval,
        UnrestrictedGrantPolicy unrestrictedPolicy)
    {
        this.groupRepository = groupRepository;
        this.approval = approval;
        this.unrestrictedPolicy = unrestrictedPolicy;
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
        if (!group.Memberships.Any(member => member.UserId == command.UserId) &&
            await this.unrestrictedPolicy.GroupIsUnrestrictedAsync(group, cancellationToken))
        {
            Result approvalResult = await this.approval.RequireAsync("Group.AddUnrestrictedMember", $"group:{group.Id.Value}/user:{command.UserId.Value}", cancellationToken: cancellationToken);
            if (approvalResult.IsFailure) { return approvalResult.Error; }
        }

        Result addResult = group.AddMember(command.UserId, command.AssignedBy, this.dateTimeProvider);
        if (addResult.IsFailure)
        {
            return addResult;
        }

        await this.groupRepository.SaveChangesAsync(cancellationToken);
        await this.approval.RecordOutcomeAsync(true, cancellationToken);

        return Result.Success();
    }
}
