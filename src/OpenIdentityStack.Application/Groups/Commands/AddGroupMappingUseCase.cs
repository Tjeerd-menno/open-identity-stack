using OpenIdentityStack.Application.Authorization;
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
    private readonly IAdministrativeApproval approval;
    private readonly UnrestrictedGrantPolicy unrestrictedPolicy;
    private readonly IDateTimeProvider dateTimeProvider;

    public AddGroupMappingUseCase(
        IGroupRepository groupRepository,
        IDateTimeProvider dateTimeProvider,
        IAdministrativeApproval approval,
        UnrestrictedGrantPolicy unrestrictedPolicy)
    {
        this.groupRepository = groupRepository;
        this.approval = approval;
        this.unrestrictedPolicy = unrestrictedPolicy;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> ExecuteAsync(AddGroupMappingCommand command, CancellationToken cancellationToken = default)
    {
        Group? group = await this.groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return new DomainError("Group.NotFound", "Group not found.");
        }

        if (command.Type == MappingType.Role && await this.unrestrictedPolicy.RoleIsUnrestrictedAsync(command.Target, cancellationToken))
        {
            Result approvalResult = await this.approval.RequireAsync("Group.MapUnrestrictedRole", $"group:{group.Id.Value}/role:{command.Target}", cancellationToken: cancellationToken);
            if (approvalResult.IsFailure) { return approvalResult.Error; }
        }

        Result addResult = group.AddMapping(
            command.Type,
            command.Target,
            command.Value,
            command.TokenTarget,
            this.dateTimeProvider);
        if (addResult.IsFailure)
        {
            return addResult;
        }

        await this.groupRepository.SaveChangesAsync(cancellationToken);
        await this.approval.RecordOutcomeAsync(true, cancellationToken);

        return Result.Success();
    }
}
