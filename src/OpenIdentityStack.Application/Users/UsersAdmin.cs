using OpenIdentityStack.Application.Groups;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Application.Users;

internal sealed class UsersAdmin : IUsersAdmin
{
    private readonly ICreateUserUseCase createUser;
    private readonly IGetUserQueryHandler getUser;
    private readonly IListUsersQueryHandler listUsers;
    private readonly IUpdateUserUseCase updateUser;
    private readonly IDeleteUserUseCase deleteUser;
    private readonly IDisableUserUseCase disableUser;
    private readonly IEnableUserUseCase enableUser;
    private readonly IResetPasswordUseCase resetPassword;
    private readonly IGetUserRolesQueryHandler getUserRoles;
    private readonly IGetUserGroupsQueryHandler getUserGroups;
    private readonly IAssignRoleUseCase assignRole;
    private readonly IUnassignRoleUseCase unassignRole;
    private readonly IIdentityMigrationInventoryQueryHandler identityMigrationInventory;
    private readonly IListUserUpstreamIdentitiesQueryHandler listUpstreamIdentities;
    private readonly ILinkUpstreamIdentityUseCase linkUpstreamIdentity;
    private readonly IUnlinkUpstreamIdentityUseCase unlinkUpstreamIdentity;
    private readonly IGetPublicProfileQueryHandler publicProfiles;

    internal UsersAdmin(
        ICreateUserUseCase createUser,
        IGetUserQueryHandler getUser,
        IListUsersQueryHandler listUsers,
        IUpdateUserUseCase updateUser,
        IDeleteUserUseCase deleteUser,
        IDisableUserUseCase disableUser,
        IEnableUserUseCase enableUser,
        IResetPasswordUseCase resetPassword,
        IGetUserRolesQueryHandler getUserRoles,
        IGetUserGroupsQueryHandler getUserGroups,
        IAssignRoleUseCase assignRole,
        IUnassignRoleUseCase unassignRole,
        IIdentityMigrationInventoryQueryHandler identityMigrationInventory,
        IListUserUpstreamIdentitiesQueryHandler listUpstreamIdentities,
        ILinkUpstreamIdentityUseCase linkUpstreamIdentity,
        IUnlinkUpstreamIdentityUseCase unlinkUpstreamIdentity,
        IGetPublicProfileQueryHandler publicProfiles)
    {
        this.createUser = createUser;
        this.getUser = getUser;
        this.listUsers = listUsers;
        this.updateUser = updateUser;
        this.deleteUser = deleteUser;
        this.disableUser = disableUser;
        this.enableUser = enableUser;
        this.resetPassword = resetPassword;
        this.getUserRoles = getUserRoles;
        this.getUserGroups = getUserGroups;
        this.assignRole = assignRole;
        this.unassignRole = unassignRole;
        this.identityMigrationInventory = identityMigrationInventory;
        this.listUpstreamIdentities = listUpstreamIdentities;
        this.linkUpstreamIdentity = linkUpstreamIdentity;
        this.unlinkUpstreamIdentity = unlinkUpstreamIdentity;
        this.publicProfiles = publicProfiles;
    }

    public Task<Result<CreateUserResult>> CreateAsync(CreateUserCommand command, CancellationToken cancellationToken = default) =>
        this.createUser.ExecuteAsync(command, cancellationToken);

    public Task<Result<GetUserResult>> GetAsync(GetUserQuery query, CancellationToken cancellationToken = default) =>
        this.getUser.HandleAsync(query, cancellationToken);

    public Task<Result<ListUsersResult>> ListAsync(ListUsersQuery query, CancellationToken cancellationToken = default) =>
        this.listUsers.HandleAsync(query, cancellationToken);

    public Task<Result<UpdateUserResult>> UpdateAsync(UpdateUserCommand command, CancellationToken cancellationToken = default) =>
        this.updateUser.ExecuteAsync(command, cancellationToken);

    public Task<Result> DeleteAsync(DeleteUserCommand command, CancellationToken cancellationToken = default) =>
        this.deleteUser.ExecuteAsync(command, cancellationToken);

    public Task<Result<DisableUserResult>> DisableAsync(DisableUserCommand command, CancellationToken cancellationToken = default) =>
        this.disableUser.ExecuteAsync(command, cancellationToken);

    public Task<Result<EnableUserResult>> EnableAsync(EnableUserCommand command, CancellationToken cancellationToken = default) =>
        this.enableUser.ExecuteAsync(command, cancellationToken);

    public Task<Result<ResetPasswordResult>> ResetPasswordAsync(ResetPasswordCommand command, CancellationToken cancellationToken = default) =>
        this.resetPassword.ExecuteAsync(command, cancellationToken);

    public Task<Result<GetUserRolesResponse>> GetRolesAsync(UserId userId, bool activeOnly = true, CancellationToken cancellationToken = default) =>
        this.getUserRoles.HandleAsync(userId, activeOnly, cancellationToken);

    public Task<Result<List<GroupResponse>>> GetGroupsAsync(UserId userId, CancellationToken cancellationToken = default) =>
        this.getUserGroups.HandleAsync(userId, cancellationToken);

    public Task<Result> AssignRoleAsync(AssignRoleCommand command, CancellationToken cancellationToken = default) =>
        this.assignRole.ExecuteAsync(command, cancellationToken);

    public Task<Result> UnassignRoleAsync(UnassignRoleCommand command, CancellationToken cancellationToken = default) =>
        this.unassignRole.ExecuteAsync(command, cancellationToken);

    public Task<Result<IdentityMigrationInventoryResult>> GetIdentityMigrationInventoryAsync(
        IdentityMigrationInventoryQuery query,
        CancellationToken cancellationToken = default) =>
        this.identityMigrationInventory.ExecuteAsync(query, cancellationToken);

    public Task<Result<ListUserUpstreamIdentitiesResult>> ListUpstreamIdentitiesAsync(
        ListUserUpstreamIdentitiesQuery query,
        CancellationToken cancellationToken = default) =>
        this.listUpstreamIdentities.ExecuteAsync(query, cancellationToken);

    public Task<Result<LinkUpstreamIdentityResult>> LinkUpstreamIdentityAsync(
        LinkUpstreamIdentityCommand command,
        CancellationToken cancellationToken = default) =>
        this.linkUpstreamIdentity.ExecuteAsync(command, cancellationToken);

    public Task<Result<UnlinkUpstreamIdentityResult>> UnlinkUpstreamIdentityAsync(
        UnlinkUpstreamIdentityCommand command,
        CancellationToken cancellationToken = default) =>
        this.unlinkUpstreamIdentity.ExecuteAsync(command, cancellationToken);

    public Task<PublicProfileResult?> GetPublicProfileAsync(GetPublicProfileQuery query, CancellationToken cancellationToken = default) =>
        this.publicProfiles.HandleAsync(query, cancellationToken);
}
