using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Roles.Queries;
using SharedKernel;

namespace OpenIdentityStack.Application.Roles;

/// <summary>
/// Role administration the API may call. Role use cases stay behind this type.
/// </summary>
public interface IRolesAdmin
{
    Task<Result<ListRolesResponse>> ListAsync(ListRolesQuery query, CancellationToken cancellationToken = default);

    Task<Result<RoleDto>> GetAsync(GetRoleQuery query, CancellationToken cancellationToken = default);

    Task<Result<CreateRoleResponse>> CreateAsync(CreateRoleCommand command, CancellationToken cancellationToken = default);

    Task<Result<RoleDto>> UpdateAsync(UpdateRoleCommand command, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(DeleteRoleCommand command, CancellationToken cancellationToken = default);

    Task<Result<RoleDto>> DisableAsync(DisableRoleCommand command, CancellationToken cancellationToken = default);

    Task<Result<RoleDto>> EnableAsync(EnableRoleCommand command, CancellationToken cancellationToken = default);

    Task<Result<RoleDto>> SetPermissionsAsync(SetRolePermissionsCommand command, CancellationToken cancellationToken = default);

    Task<Result<RoleDto>> AddPermissionAsync(AddRolePermissionCommand command, CancellationToken cancellationToken = default);

    Task<Result<RoleDto>> RemovePermissionAsync(RemoveRolePermissionCommand command, CancellationToken cancellationToken = default);
}
