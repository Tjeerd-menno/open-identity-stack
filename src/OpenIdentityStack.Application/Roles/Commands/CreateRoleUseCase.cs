
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Roles.Commands;
/// <summary>
/// Use case interface for creating roles.
/// </summary>
public interface ICreateRoleUseCase
{
    /// <summary>
    /// Executes the create role command.
    /// </summary>
    /// <param name="command">The command containing role details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the response or an error.</returns>
    Task<Result<CreateRoleResponse>> ExecuteAsync(CreateRoleCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case for creating a new role.
/// </summary>
public sealed class CreateRoleUseCase : ICreateRoleUseCase
{
    private readonly IRoleRepository roleRepository;
    private readonly IPermissionAssignmentValidator permissionAssignmentValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateRoleUseCase"/> class.
    /// </summary>
    /// <param name="roleRepository">The role repository.</param>
    internal CreateRoleUseCase(IRoleRepository roleRepository)
        : this(roleRepository, new AllowAllPermissionAssignmentValidator())
    {
    }

    public CreateRoleUseCase(
        IRoleRepository roleRepository,
        IPermissionAssignmentValidator permissionAssignmentValidator)
    {
        this.roleRepository = roleRepository;
        this.permissionAssignmentValidator = permissionAssignmentValidator;
    }

    /// <inheritdoc />
    public async Task<Result<CreateRoleResponse>> ExecuteAsync(
        CreateRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        // Validate and create the role with display name
        Result<Role> roleResult = Role.Create(command.Name, command.DisplayName, command.Description);
        if (roleResult.IsFailure)
        {
            return roleResult.Error;
        }

        Role role = roleResult.Value;

        // Check if role name already exists
        bool exists = await this.roleRepository.ExistsByNameAsync(role.Name, cancellationToken);
        if (exists)
        {
            return DomainError.Conflict("NameAlreadyExists", $"A role with the name '{role.Name}' already exists.");
        }

        // Set permissions if provided
        if (command.Permissions is { Count: > 0 })
        {
            Result validationResult = await this.permissionAssignmentValidator
                .ValidateAssignableAsync(command.Permissions, cancellationToken)
                .ConfigureAwait(false);
            if (validationResult.IsFailure)
            {
                return validationResult.Error;
            }

            role.SetPermissions(command.Permissions);
        }

        // Persist the role
        await this.roleRepository.AddAsync(role, cancellationToken);
        await this.roleRepository.SaveChangesAsync(cancellationToken);

        return new CreateRoleResponse(
            role.Id.Value,
            role.Name,
            role.DisplayName,
            role.Description,
            role.IsActive);
    }

    private sealed class AllowAllPermissionAssignmentValidator : IPermissionAssignmentValidator
    {
        public Task<Result> ValidateAssignableAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }
    }
}
