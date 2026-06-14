using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Roles.Commands;

/// <summary>
/// Use case interface for disabling a role.
/// </summary>
public interface IDisableRoleUseCase
{
    /// <summary>
    /// Executes the disable role command.
    /// </summary>
    /// <param name="command">The command identifying the role to disable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the disabled role or an error.</returns>
    Task<Result<RoleDto>> ExecuteAsync(DisableRoleCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case for disabling a role.
/// </summary>
public sealed class DisableRoleUseCase : IDisableRoleUseCase
{
    private readonly IRoleRepository roleRepository;

    public DisableRoleUseCase(IRoleRepository roleRepository)
    {
        this.roleRepository = roleRepository;
    }

    /// <inheritdoc />
    public async Task<Result<RoleDto>> ExecuteAsync(
        DisableRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        Role? role = await this.roleRepository.GetByIdAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return RoleErrors.NotFound;
        }

        Result result = role.Disable();
        if (result.IsFailure)
        {
            return result.Error;
        }

        await this.roleRepository.SaveChangesAsync(cancellationToken);

        return RoleDtoMapper.ToDto(role);
    }
}
