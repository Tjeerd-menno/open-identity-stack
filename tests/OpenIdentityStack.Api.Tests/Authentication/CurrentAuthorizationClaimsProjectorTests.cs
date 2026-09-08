using System.Security.Claims;

using OpenIdentityStack.Api.Authentication;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Roles;
using OpenIddict.Abstractions;

using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Authentication;

public sealed class CurrentAuthorizationClaimsProjectorTests
{
    [Fact]
    public async Task RefreshAsync_ProjectsPermissionsWithinIssuedAndCurrentResourceGrants()
    {
        IGetUserEffectiveRolesQueryHandler roles = Substitute.For<IGetUserEffectiveRolesQueryHandler>();
        IResourcePermissionService resources = Substitute.For<IResourcePermissionService>();
        var userId = UserId.Create();
        Result<IReadOnlyList<RoleDto>> rolesResult =
            new List<RoleDto>
            {
                new(Guid.NewGuid(), "current-role", "Current", null, true, true, ["issued.and.granted", "current.only"]),
                new(Guid.NewGuid(), "inactive-role", "Inactive", null, false, false, ["inactive.permission"])
            };
        roles.HandleAsync(userId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(rolesResult));
        resources.ProjectAsync(Arg.Any<ResourceTokenRequest>(), Arg.Any<CancellationToken>())
            .Returns((Result<ResourceTokenProjection>)new ResourceTokenProjection(
                ["https://admin.example.com"],
                ["issued.and.granted"],
                new Dictionary<Guid, long>()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, "stale-role"),
            new Claim("permission", "issued.and.granted"),
            new Claim("permission", "issued.but.revoked"),
            new Claim("permissions", "stale.plural.permission"),
            new Claim("client_id", "admin-client"),
            new Claim("scope", "admin"),
            new Claim("aud", "https://admin.example.com"),
            new Claim("iss", "https://issuer.example.com")
        ], "Bearer"));
        principal.SetScopes("admin");
        principal.SetResources("https://admin.example.com");

        var projector = new CurrentAuthorizationClaimsProjector(roles, resources);

        await projector.RefreshAsync(principal, userId);

        Assert.DoesNotContain(principal.Claims, claim => claim.Value is "stale-role" or "issued.but.revoked"
            or "stale.plural.permission" or "current.only" or "inactive.permission");
        Assert.Contains(principal.Claims, claim => claim.Type == "role" && claim.Value == "current-role");
        Assert.Contains(principal.Claims, claim => claim.Type == "permission" && claim.Value == "issued.and.granted");
        Assert.Contains(principal.Claims, claim => claim.Type == "scope" && claim.Value == "admin");
        Assert.Contains(principal.Claims, claim => claim.Type == "aud" && claim.Value == "https://admin.example.com");
        Assert.Contains(principal.Claims, claim => claim.Type == "iss" && claim.Value == "https://issuer.example.com");
        await resources.Received(1).ProjectAsync(
            Arg.Is<ResourceTokenRequest>(request => request.ClientId == "admin-client"
                && request.Scopes.Count == 1 && request.Scopes.Contains("admin")
                && request.RequestedResources.Count == 1 && request.RequestedResources.Contains("https://admin.example.com")
                && request.OriginalPermissions!.Count == 2
                && request.OriginalPermissions.Contains("issued.and.granted")
                && request.OriginalPermissions.Contains("issued.but.revoked")
                && request.OriginalAudiences!.Count == 1 && request.OriginalAudiences.Contains("https://admin.example.com")
                && request.UserPermissions!.Count == 2
                && request.UserPermissions.Contains("issued.and.granted")
                && request.UserPermissions.Contains("current.only")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_WhenRoleQueryFails_RemovesStaleAuthorizationClaims()
    {
        IGetUserEffectiveRolesQueryHandler roles = Substitute.For<IGetUserEffectiveRolesQueryHandler>();
        IResourcePermissionService resources = Substitute.For<IResourcePermissionService>();
        roles.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)DomainError.Forbidden("Authorization.Unavailable", "Role grants are unavailable."));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, "stale-role"),
            new Claim("permission", "stale.permission"),
            new Claim("permissions", "stale.alias"),
            new Claim("iss", "https://issuer.example.com")
        ], "Bearer"));

        await new CurrentAuthorizationClaimsProjector(roles, resources).RefreshAsync(principal, UserId.Create());

        Assert.DoesNotContain(principal.Claims, claim => claim.Type is ClaimTypes.Role or "permission" or "permissions");
        Assert.Contains(principal.Claims, claim => claim.Type == "iss" && claim.Value == "https://issuer.example.com");
        await resources.DidNotReceive().ProjectAsync(Arg.Any<ResourceTokenRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_WhenResourceProjectionFails_RemovesStaleAuthorizationClaims()
    {
        IGetUserEffectiveRolesQueryHandler roles = Substitute.For<IGetUserEffectiveRolesQueryHandler>();
        IResourcePermissionService resources = Substitute.For<IResourcePermissionService>();
        roles.HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(
            (Result<IReadOnlyList<RoleDto>>)new List<RoleDto>
            {
                new(Guid.NewGuid(), "current-role", "Current", null, false, true, ["current.permission"])
            });
        resources.ProjectAsync(Arg.Any<ResourceTokenRequest>(), Arg.Any<CancellationToken>())
            .Returns((Result<ResourceTokenProjection>)DomainError.Forbidden("Authorization.Unavailable", "Resource grants are unavailable."));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, "stale-role"),
            new Claim("permission", "stale.permission"),
            new Claim("permissions", "stale.alias"),
            new Claim("iss", "https://issuer.example.com")
        ], "Bearer"));

        await new CurrentAuthorizationClaimsProjector(roles, resources).RefreshAsync(principal, UserId.Create());

        Assert.DoesNotContain(principal.Claims, claim => claim.Type is ClaimTypes.Role or "permission" or "permissions");
        Assert.Contains(principal.Claims, claim => claim.Type == "iss" && claim.Value == "https://issuer.example.com");
    }
}
