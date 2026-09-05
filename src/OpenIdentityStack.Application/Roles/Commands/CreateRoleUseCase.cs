using OpenIdentityStack.Application.Authorization;

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
    private readonly IAdministrativeApproval approval;
    private readonly IPermissionAssignmentValidator permissionAssignmentValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateRoleUseCase"/> class.
    /// </summary>
    /// <param name="roleRepository">The role repository.</param>
    public CreateRoleUseCase(
        IRoleRepository roleRepository,
        IPermissionAssignmentValidator permissionAssignmentValidator,
        IAdministrativeApproval approval)
    {
        this.roleRepository = roleRepository;
        this.approval = approval;
        this.permissionAssignmentValidator = permissionAssignmentValidator;
    }

    /// <inheritdoc />
    public async Task<Result<CreateRoleResponse>> ExecuteAsync(
        CreateRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        await this.approval.CaptureAuthorityAsync(cancellationToken);
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
            if (UnrestrictedGrantPolicy.IncludesAllPermissions(command.Permissions))
            {
                Result approvalResult = await this.approval.RequireAsync("Role.CreateUnrestricted", role.Id.Value.ToString(), command.AcknowledgeWildcardGrant, cancellationToken);
                if (approvalResult.IsFailure) { return approvalResult.Error; }
            }

            Result validationResult = await this.permissionAssignmentValidator
                .ValidateAssignableAsync(command.Permissions, command.AcknowledgeWildcardGrant, cancellationToken)
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
        await this.approval.RecordOutcomeAsync(true, cancellationToken);

        return new CreateRoleResponse(
            role.Id.Value,
            role.Name,
            role.DisplayName,
            role.Description,
            role.IsActive,
            role.Permissions);
    }

}
