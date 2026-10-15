using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Roles.Queries;
using SharedKernel;

namespace OpenIdentityStack.Application.Roles;

internal sealed class RolesAdmin : IRolesAdmin
{
    private readonly IListRolesQueryHandler listRoles;
    private readonly IGetRoleQueryHandler getRole;
    private readonly ICreateRoleUseCase createRole;
    private readonly IUpdateRoleUseCase updateRole;
    private readonly IDeleteRoleUseCase deleteRole;
    private readonly IDisableRoleUseCase disableRole;
    private readonly IEnableRoleUseCase enableRole;
    private readonly ISetRolePermissionsUseCase setPermissions;
    private readonly IAddRolePermissionUseCase addPermission;
    private readonly IRemoveRolePermissionUseCase removePermission;

    internal RolesAdmin(
        IListRolesQueryHandler listRoles,
        IGetRoleQueryHandler getRole,
        ICreateRoleUseCase createRole,
        IUpdateRoleUseCase updateRole,
        IDeleteRoleUseCase deleteRole,
        IDisableRoleUseCase disableRole,
        IEnableRoleUseCase enableRole,
        ISetRolePermissionsUseCase setPermissions,
        IAddRolePermissionUseCase addPermission,
        IRemoveRolePermissionUseCase removePermission)
    {
        this.listRoles = listRoles;
        this.getRole = getRole;
        this.createRole = createRole;
        this.updateRole = updateRole;
        this.deleteRole = deleteRole;
        this.disableRole = disableRole;
        this.enableRole = enableRole;
        this.setPermissions = setPermissions;
        this.addPermission = addPermission;
        this.removePermission = removePermission;
    }

    public Task<Result<ListRolesResponse>> ListAsync(ListRolesQuery query, CancellationToken cancellationToken = default) =>
        this.listRoles.HandleAsync(query, cancellationToken);

    public Task<Result<RoleDto>> GetAsync(GetRoleQuery query, CancellationToken cancellationToken = default) =>
        this.getRole.HandleAsync(query, cancellationToken);

    public Task<Result<CreateRoleResponse>> CreateAsync(CreateRoleCommand command, CancellationToken cancellationToken = default) =>
        this.createRole.ExecuteAsync(command, cancellationToken);

    public Task<Result<RoleDto>> UpdateAsync(UpdateRoleCommand command, CancellationToken cancellationToken = default) =>
        this.updateRole.ExecuteAsync(command, cancellationToken);

    public Task<Result> DeleteAsync(DeleteRoleCommand command, CancellationToken cancellationToken = default) =>
        this.deleteRole.ExecuteAsync(command, cancellationToken);

    public Task<Result<RoleDto>> DisableAsync(DisableRoleCommand command, CancellationToken cancellationToken = default) =>
        this.disableRole.ExecuteAsync(command, cancellationToken);

    public Task<Result<RoleDto>> EnableAsync(EnableRoleCommand command, CancellationToken cancellationToken = default) =>
        this.enableRole.ExecuteAsync(command, cancellationToken);

    public Task<Result<RoleDto>> SetPermissionsAsync(SetRolePermissionsCommand command, CancellationToken cancellationToken = default) =>
        this.setPermissions.ExecuteAsync(command, cancellationToken);

    public Task<Result<RoleDto>> AddPermissionAsync(AddRolePermissionCommand command, CancellationToken cancellationToken = default) =>
        this.addPermission.ExecuteAsync(command, cancellationToken);

    public Task<Result<RoleDto>> RemovePermissionAsync(RemoveRolePermissionCommand command, CancellationToken cancellationToken = default) =>
        this.removePermission.ExecuteAsync(command, cancellationToken);
}
