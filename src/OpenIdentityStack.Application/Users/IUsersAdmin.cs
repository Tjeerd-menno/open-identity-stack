using OpenIdentityStack.Application.Groups;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Application.Users;

/// <summary>
/// Admin and public user operations the API may call. Users use cases stay behind this type.
/// </summary>
public interface IUsersAdmin
{
    Task<Result<CreateUserResult>> CreateAsync(CreateUserCommand command, CancellationToken cancellationToken = default);

    Task<Result<GetUserResult>> GetAsync(GetUserQuery query, CancellationToken cancellationToken = default);

    Task<Result<ListUsersResult>> ListAsync(ListUsersQuery query, CancellationToken cancellationToken = default);

    Task<Result<UpdateUserResult>> UpdateAsync(UpdateUserCommand command, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(DeleteUserCommand command, CancellationToken cancellationToken = default);

    Task<Result<DisableUserResult>> DisableAsync(DisableUserCommand command, CancellationToken cancellationToken = default);

    Task<Result<EnableUserResult>> EnableAsync(EnableUserCommand command, CancellationToken cancellationToken = default);

    Task<Result<ResetPasswordResult>> ResetPasswordAsync(ResetPasswordCommand command, CancellationToken cancellationToken = default);

    Task<Result<GetUserRolesResponse>> GetRolesAsync(UserId userId, bool activeOnly = true, CancellationToken cancellationToken = default);

    Task<Result<List<GroupResponse>>> GetGroupsAsync(UserId userId, CancellationToken cancellationToken = default);

    Task<Result> AssignRoleAsync(AssignRoleCommand command, CancellationToken cancellationToken = default);

    Task<Result> UnassignRoleAsync(UnassignRoleCommand command, CancellationToken cancellationToken = default);

    Task<Result<IdentityMigrationInventoryResult>> GetIdentityMigrationInventoryAsync(
        IdentityMigrationInventoryQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<ListUserUpstreamIdentitiesResult>> ListUpstreamIdentitiesAsync(
        ListUserUpstreamIdentitiesQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<LinkUpstreamIdentityResult>> LinkUpstreamIdentityAsync(
        LinkUpstreamIdentityCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<UnlinkUpstreamIdentityResult>> UnlinkUpstreamIdentityAsync(
        UnlinkUpstreamIdentityCommand command,
        CancellationToken cancellationToken = default);

    Task<PublicProfileResult?> GetPublicProfileAsync(GetPublicProfileQuery query, CancellationToken cancellationToken = default);
}
