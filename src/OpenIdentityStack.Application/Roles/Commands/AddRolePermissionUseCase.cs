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
    private readonly IPermissionAssignmentValidator permissionAssignmentValidator;

    public AddRolePermissionUseCase(
        IRoleRepository roleRepository,
        IPermissionAssignmentValidator permissionAssignmentValidator)
    {
        this.roleRepository = roleRepository;
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

        return RoleDtoMapper.ToDto(role);
    }
}
