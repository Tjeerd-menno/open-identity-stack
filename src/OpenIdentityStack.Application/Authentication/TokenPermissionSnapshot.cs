using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Queries;
using SharedKernel;

namespace OpenIdentityStack.Application.Authentication;

internal sealed class TokenPermissionSnapshot : ITokenPermissionSnapshot
{
    private readonly IGetUserEffectiveRolesQueryHandler roles;

    public TokenPermissionSnapshot(IGetUserEffectiveRolesQueryHandler roles)
    {
        this.roles = roles;
    }

    public async Task<Result<TokenPermissionSnapshotResult>> GetAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<RoleDto>> result = await this.roles.HandleAsync(userId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        IReadOnlyList<string> permissions = result.Value
            .Where(static role => role.IsActive)
            .SelectMany(static role => role.Permissions)
            .ToArray();
        return new TokenPermissionSnapshotResult(result.Value, permissions);
    }
}
