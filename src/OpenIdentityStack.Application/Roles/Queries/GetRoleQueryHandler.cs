using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Roles.Queries;

/// <summary>
/// Use case interface for retrieving a single role.
/// </summary>
public interface IGetRoleQueryHandler
{
    /// <summary>
    /// Executes the get role query.
    /// </summary>
    /// <param name="query">The query identifying the role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the role or an error.</returns>
    Task<Result<RoleDto>> HandleAsync(GetRoleQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handler for the <see cref="GetRoleQuery"/>.
/// </summary>
public sealed class GetRoleQueryHandler : IGetRoleQueryHandler
{
    private readonly IRoleRepository roleRepository;

    public GetRoleQueryHandler(IRoleRepository roleRepository)
    {
        this.roleRepository = roleRepository;
    }

    /// <inheritdoc />
    public async Task<Result<RoleDto>> HandleAsync(
        GetRoleQuery query,
        CancellationToken cancellationToken = default)
    {
        Role? role = await this.roleRepository.GetByIdAsync(query.RoleId, cancellationToken);
        if (role is null)
        {
            return RoleErrors.NotFound;
        }

        return RoleDtoMapper.ToDto(role);
    }
}
