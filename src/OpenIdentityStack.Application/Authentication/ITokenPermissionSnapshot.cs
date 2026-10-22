using OpenIdentityStack.Application.Roles.Queries;
using SharedKernel;

namespace OpenIdentityStack.Application.Authentication;

/// <summary>
/// The token path's one read of a user's effective permissions.
/// </summary>
public interface ITokenPermissionSnapshot
{
    Task<Result<TokenPermissionSnapshotResult>> GetAsync(UserId userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Roles and the active permissions those roles grant, from a single resolution.
/// </summary>
public sealed record TokenPermissionSnapshotResult(
    IReadOnlyList<RoleDto> Roles,
    IReadOnlyList<string> Permissions);
