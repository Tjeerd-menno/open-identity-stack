using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Roles.Commands;

/// <summary>
/// Use case interface for enabling a role.
/// </summary>
public interface IEnableRoleUseCase
{
    /// <summary>
    /// Executes the enable role command.
    /// </summary>
    /// <param name="command">The command identifying the role to enable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the enabled role or an error.</returns>
    Task<Result<RoleDto>> ExecuteAsync(EnableRoleCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case for enabling a previously disabled role.
/// </summary>
public sealed class EnableRoleUseCase : IEnableRoleUseCase
{
    private readonly IRoleRepository roleRepository;

    public EnableRoleUseCase(IRoleRepository roleRepository)
    {
        this.roleRepository = roleRepository;
    }

    /// <inheritdoc />
    public async Task<Result<RoleDto>> ExecuteAsync(
        EnableRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        Role? role = await this.roleRepository.GetByIdAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return RoleErrors.NotFound;
        }

        Result result = role.Enable();
        if (result.IsFailure)
        {
            return result.Error;
        }

        await this.roleRepository.SaveChangesAsync(cancellationToken);

        return RoleDtoMapper.ToDto(role);
    }
}
