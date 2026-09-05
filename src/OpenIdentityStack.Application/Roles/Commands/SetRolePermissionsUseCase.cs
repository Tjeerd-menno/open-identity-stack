using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Roles.Commands;

/// <summary>
/// Use case interface for replacing the permissions on a role.
/// </summary>
public interface ISetRolePermissionsUseCase
{
    /// <summary>
    /// Executes the set role permissions command.
    /// </summary>
    /// <param name="command">The command containing the permissions to set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the updated role or an error.</returns>
    Task<Result<RoleDto>> ExecuteAsync(SetRolePermissionsCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case for replacing all permissions on a role.
/// </summary>
public sealed class SetRolePermissionsUseCase : ISetRolePermissionsUseCase
{
    private readonly IRoleRepository roleRepository;
    private readonly IAdministrativeApproval approval;
    private readonly IPermissionAssignmentValidator permissionAssignmentValidator;

    public SetRolePermissionsUseCase(
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
        SetRolePermissionsCommand command,
        CancellationToken cancellationToken = default)
    {
        await this.approval.CaptureAuthorityAsync(cancellationToken);
        Role? role = await this.roleRepository.GetByIdAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return RoleErrors.NotFound;
        }

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
        await this.roleRepository.SaveChangesAsync(cancellationToken);
        await this.approval.RecordOutcomeAsync(true, cancellationToken);

        return RoleDtoMapper.ToDto(role);
    }
}
