using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Roles.Commands;

/// <summary>
/// Use case interface for updating a role.
/// </summary>
public interface IUpdateRoleUseCase
{
    /// <summary>
    /// Executes the update role command.
    /// </summary>
    /// <param name="command">The command containing the updated role details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the updated role or an error.</returns>
    Task<Result<RoleDto>> ExecuteAsync(UpdateRoleCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case for updating a role.
/// </summary>
public sealed class UpdateRoleUseCase : IUpdateRoleUseCase
{
    private readonly IRoleRepository roleRepository;
    private readonly IAdministrativeApproval approval;
    private readonly IPermissionAssignmentValidator permissionAssignmentValidator;

    public UpdateRoleUseCase(
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
        UpdateRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        Role? role = await this.roleRepository.GetByIdAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return RoleErrors.NotFound;
        }

        if (command.Permissions is not null)
        {
            IReadOnlyList<string> newPermissions = command.Permissions
                .Except(role.Permissions, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (UnrestrictedGrantPolicy.IncludesAllPermissions(newPermissions))
            {
                Result approvalResult = await this.approval.RequireAsync("Role.GrantUnrestricted", role.Id.Value.ToString(), command.AcknowledgeWildcardGrant, cancellationToken);
                if (approvalResult.IsFailure) { return approvalResult.Error; }
            }
            Result validationResult = await this.permissionAssignmentValidator.ValidateAssignableAsync(
                newPermissions,
                command.AcknowledgeWildcardGrant,
                cancellationToken);
            if (validationResult.IsFailure)
            {
                return validationResult.Error;
            }

            role.SetPermissions(command.Permissions);
        }

        role.UpdateDescription(command.Description);
        if (command.DisplayName is not null) { role.UpdateDisplayName(command.DisplayName); }
        await this.roleRepository.SaveChangesAsync(cancellationToken);
        await this.approval.RecordOutcomeAsync(true, cancellationToken);

        return RoleDtoMapper.ToDto(role);
    }
}
