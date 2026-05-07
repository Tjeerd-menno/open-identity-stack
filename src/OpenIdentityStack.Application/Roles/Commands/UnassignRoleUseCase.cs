
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Roles.Commands;
/// <summary>
/// Use case interface for unassigning roles.
/// </summary>
public interface IUnassignRoleUseCase
{
    /// <summary>
    /// Executes the unassign role command.
    /// </summary>
    /// <param name="command">The command containing user and role IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> ExecuteAsync(UnassignRoleCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case for unassigning a role from a user.
/// </summary>
public sealed class UnassignRoleUseCase : IUnassignRoleUseCase
{
    private readonly IUserRepository userRepository;
    private readonly IRoleRepository roleRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnassignRoleUseCase"/> class.
    /// </summary>
    /// <param name="userRepository">The user repository.</param>
    /// <param name="roleRepository">The role repository.</param>
    public UnassignRoleUseCase(
        IUserRepository userRepository,
        IRoleRepository roleRepository)
    {
        this.userRepository = userRepository;
        this.roleRepository = roleRepository;
    }

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(
        UnassignRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        // Validate user exists
        User? user = await this.userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(
                DomainError.NotFound("UserNotFound", "User not found."));
        }

        // Check if the role is assigned
        bool isAssigned = await this.roleRepository.IsRoleAssignedAsync(
            command.UserId,
            command.RoleId,
            cancellationToken);

        if (!isAssigned)
        {
            return Result.Failure(
                DomainError.NotFound("RoleNotAssigned", "Role is not assigned to this user."));
        }

        // Remove the assignment
        await this.roleRepository.RemoveRoleAssignmentAsync(
            command.UserId,
            command.RoleId,
            cancellationToken);
        await this.roleRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
