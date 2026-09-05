using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Roles.Commands;

/// <summary>
/// Use case interface for adding a single permission to a role.
/// </summary>
public interface IAddRolePermissionUseCase
{
    /// <summary>
    /// Executes the add role permission command.
    /// </summary>
    /// <param name="command">The command containing the permission to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the updated role or an error.</returns>
    Task<Result<RoleDto>> ExecuteAsync(AddRolePermissionCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case for adding a single permission to a role.
/// </summary>
public sealed class AddRolePermissionUseCase : IAddRolePermissionUseCase
{
    private readonly IRoleRepository roleRepository;
    private readonly IAdministrativeApproval approval;
    private readonly IPermissionAssignmentValidator permissionAssignmentValidator;

    public AddRolePermissionUseCase(
        IRoleRepository roleRepository,
        IPermissionAssignmentValidator permissionAssignmentValidator,
        IAdministrativeApproval approval)
    {
        this.roleRepository = roleRepository;
        this.approval = approval;
        this.permissionAssignmentValidator = permissionAssignmentValidator;
    }

    /// <inheritdoc />
    public async Task<Result<RoleDto>> ExecuteAsync(
        AddRolePermissionCommand command,
        CancellationToken cancellationToken = default)
    {
        Role? role = await this.roleRepository.GetByIdAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return RoleErrors.NotFound;
        }

        if (UnrestrictedGrantPolicy.IncludesAllPermissions([command.Permission]) &&
            !UnrestrictedGrantPolicy.IncludesAllPermissions(role.Permissions))
        {
            Result approvalResult = await this.approval.RequireAsync("Role.GrantUnrestricted", role.Id.Value.ToString(), command.AcknowledgeWildcardGrant, cancellationToken);
            if (approvalResult.IsFailure) { return approvalResult.Error; }
        }

        Result validationResult = await this.permissionAssignmentValidator.ValidateAssignableAsync(
            [command.Permission],
            command.AcknowledgeWildcardGrant,
            cancellationToken);
        if (validationResult.IsFailure)
        {
            return validationResult.Error;
        }

        Result result = role.AddPermission(command.Permission);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await this.roleRepository.SaveChangesAsync(cancellationToken);
        await this.approval.RecordOutcomeAsync(true, cancellationToken);

        return RoleDtoMapper.ToDto(role);
    }
}
