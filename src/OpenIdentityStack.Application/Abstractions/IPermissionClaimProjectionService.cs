using System.Security.Claims;

namespace OpenIdentityStack.Application.Abstractions;

public interface IPermissionClaimProjectionService
{
    Task<IReadOnlyList<string>> ExpandAssignedPermissionsAsync(
        IEnumerable<string> assignedPermissions,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> FilterPermissionsForCaller(
        IEnumerable<string> permissions,
        string? requestingClientId);

    IReadOnlyList<string> GetPermissionClaims(ClaimsPrincipal principal);
}
