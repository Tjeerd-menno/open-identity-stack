using System.Security.Claims;

using OpenIdentityStack.Api.Authentication;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Authentication;

public sealed class CurrentAuthorizationClaimsProjectorTests
{
    [Fact]
    public async Task RefreshAsync_ReplacesIssuedRolesAndPermissionsWithCurrentEffectiveGrants()
    {
        IGetUserEffectiveRolesQueryHandler roles = Substitute.For<IGetUserEffectiveRolesQueryHandler>();
        IPermissionClaimProjectionService permissions = Substitute.For<IPermissionClaimProjectionService>();
        var userId = UserId.Create();
        Result<IReadOnlyList<RoleDto>> rolesResult =
            new List<RoleDto> { new(Guid.NewGuid(), "current-role", "Current", null, true, false, ["current.permission"]) };
        roles.HandleAsync(userId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(rolesResult));
        permissions.ExpandAssignedPermissionsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)["current.permission"]);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, "stale-role"),
            new Claim("permission", "stale.permission"),
            new Claim("permissions", "stale.plural.permission"),
            new Claim("scope", "api")
        ], "Bearer"));

        var projector = new CurrentAuthorizationClaimsProjector(roles, permissions);

        await projector.RefreshAsync(principal, userId);

        Assert.DoesNotContain(principal.Claims, claim => claim.Value is "stale-role" or "stale.permission" or "stale.plural.permission");
        Assert.Contains(principal.Claims, claim => claim.Type == "role" && claim.Value == "current-role");
        Assert.Contains(principal.Claims, claim => claim.Type == "permission" && claim.Value == "current.permission");
        Assert.Contains(principal.Claims, claim => claim.Type == "scope" && claim.Value == "api");
    }
}
