using OpenIdentityStack.Application.Authorization;
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
    private readonly IAdministrativeApproval approval;

    public EnableRoleUseCase(IRoleRepository roleRepository,
        IAdministrativeApproval approval)
    {
        this.roleRepository = roleRepository;
        this.approval = approval;
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

        if (!role.IsActive && UnrestrictedGrantPolicy.IncludesAllPermissions(role.Permissions))
        {
            Result approvalResult = await this.approval.RequireAsync("Role.EnableUnrestricted", role.Id.Value.ToString(), cancellationToken: cancellationToken);
            if (approvalResult.IsFailure) { return approvalResult.Error; }
        }
        Result result = role.Enable();
        if (result.IsFailure)
        {
            return result.Error;
        }

        await this.roleRepository.SaveChangesAsync(cancellationToken);
        await this.approval.RecordOutcomeAsync(true, cancellationToken);

        return RoleDtoMapper.ToDto(role);
    }
}
