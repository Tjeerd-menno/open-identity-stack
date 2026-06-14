using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Roles.Commands;

/// <summary>
/// Use case interface for deleting a role.
/// </summary>
public interface IDeleteRoleUseCase
{
    /// <summary>
    /// Executes the delete role command.
    /// </summary>
    /// <param name="command">The command identifying the role to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or an error.</returns>
    Task<Result> ExecuteAsync(DeleteRoleCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case for deleting a role.
/// </summary>
public sealed class DeleteRoleUseCase : IDeleteRoleUseCase
{
    private readonly IRoleRepository roleRepository;

    public DeleteRoleUseCase(IRoleRepository roleRepository)
    {
        this.roleRepository = roleRepository;
    }

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(
        DeleteRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        Role? role = await this.roleRepository.GetByIdAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return RoleErrors.NotFound;
        }

        if (role.IsSystemRole)
        {
            return RoleErrors.CannotDeleteSystemRole;
        }

        this.roleRepository.Remove(role);
        await this.roleRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
