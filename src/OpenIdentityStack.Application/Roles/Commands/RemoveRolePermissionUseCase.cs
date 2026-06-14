using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Roles.Commands;

/// <summary>
/// Use case interface for removing a single permission from a role.
/// </summary>
public interface IRemoveRolePermissionUseCase
{
    /// <summary>
    /// Executes the remove role permission command.
    /// </summary>
    /// <param name="command">The command containing the permission to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the updated role or an error.</returns>
    Task<Result<RoleDto>> ExecuteAsync(RemoveRolePermissionCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case for removing a single permission from a role.
/// </summary>
public sealed class RemoveRolePermissionUseCase : IRemoveRolePermissionUseCase
{
    private readonly IRoleRepository roleRepository;

    public RemoveRolePermissionUseCase(IRoleRepository roleRepository)
    {
        this.roleRepository = roleRepository;
    }

    /// <inheritdoc />
    public async Task<Result<RoleDto>> ExecuteAsync(
        RemoveRolePermissionCommand command,
        CancellationToken cancellationToken = default)
    {
        Role? role = await this.roleRepository.GetByIdAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return RoleErrors.NotFound;
        }

        Result result = role.RemovePermission(command.Permission);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await this.roleRepository.SaveChangesAsync(cancellationToken);

        return RoleDtoMapper.ToDto(role);
    }
}
