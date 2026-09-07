using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Application.Authorization;

namespace OpenIdentityStack.Api.UnitTests.Authorization;

public sealed class PermissionRequirementTests
{
    [Fact]
    public void Constructor_SetsPermission()
    {
        const string permission = "users:read";
        var requirement = new PermissionRequirement(permission);
        requirement.Permission.ShouldBe(permission);
    }

    [Fact]
    public void Constructor_NullPermission_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new PermissionRequirement(null!));
    }
}

public sealed class PermissionAuthorizationHandlerTests
{
    private readonly PermissionAuthorizationHandler handler = new();

    [Fact]
    public async Task HandleRequirementAsync_UnauthenticatedUser_DoesNotSucceed()
    {
        ClaimsPrincipal user = new(new ClaimsIdentity()); // not authenticated
        PermissionRequirement requirement = new(Permissions.Users.Read);
        AuthorizationHandlerContext context = new([requirement], user, null);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithMatchingPermissionClaim_Succeeds()
    {
        Claim[] claims = [new Claim("permission", Permissions.Users.Read)];
        ClaimsPrincipal user = new(new ClaimsIdentity(claims, "test"));
        PermissionRequirement requirement = new(Permissions.Users.Read);
        AuthorizationHandlerContext context = new([requirement], user, null);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithPermissionsClaim_Succeeds()
    {
        Claim[] claims = [new Claim("permissions", Permissions.Roles.Write)];
        ClaimsPrincipal user = new(new ClaimsIdentity(claims, "test"));
        PermissionRequirement requirement = new(Permissions.Roles.Write);
        AuthorizationHandlerContext context = new([requirement], user, null);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithScopeClaim_Succeeds()
    {
        Claim[] claims = [new Claim("scope", $"{Permissions.Groups.Read} {Permissions.Groups.Write}")];
        ClaimsPrincipal user = new(new ClaimsIdentity(claims, "test"));
        PermissionRequirement requirement = new(Permissions.Groups.Read);
        AuthorizationHandlerContext context = new([requirement], user, null);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithScpClaim_Succeeds()
    {
        Claim[] claims = [new Claim("scp", Permissions.Applications.Read)];
        ClaimsPrincipal user = new(new ClaimsIdentity(claims, "test"));
        PermissionRequirement requirement = new(Permissions.Applications.Read);
        AuthorizationHandlerContext context = new([requirement], user, null);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithAdminRole_DoesNotGrantPermissions()
    {
        Claim[] claims = [new Claim(ClaimTypes.Role, "admin")];
        ClaimsPrincipal user = new(new ClaimsIdentity(claims, "test"));
        PermissionRequirement requirement = new(Permissions.Users.Delete);
        AuthorizationHandlerContext context = new([requirement], user, null);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithRoleClaim_DoesNotGrantPermissionsForAdmin()
    {
        Claim[] claims = [new Claim("role", "ADMIN")]; // case-insensitive
        ClaimsPrincipal user = new(new ClaimsIdentity(claims, "test"));
        PermissionRequirement requirement = new(Permissions.Groups.ManageMembers);
        AuthorizationHandlerContext context = new([requirement], user, null);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithUnrelatedPermission_DoesNotSucceed()
    {
        Claim[] claims = [new Claim("permission", Permissions.Users.Read)];
        ClaimsPrincipal user = new(new ClaimsIdentity(claims, "test"));
        PermissionRequirement requirement = new(Permissions.Roles.Delete);
        AuthorizationHandlerContext context = new([requirement], user, null);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithWildcardPermission_GrantsAllPermissions()
    {
        Claim[] claims = [new Claim("permission", Permissions.All)];
        ClaimsPrincipal user = new(new ClaimsIdentity(claims, "test"));
        PermissionRequirement requirement = new(Permissions.Users.Delete);
        AuthorizationHandlerContext context = new([requirement], user, null);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithResourceWildcard_GrantsResourcePermissions()
    {
        Claim[] claims = [new Claim("permission", Permissions.Users.All)];
        ClaimsPrincipal user = new(new ClaimsIdentity(claims, "test"));
        PermissionRequirement requirement = new(Permissions.Users.Write);
        AuthorizationHandlerContext context = new([requirement], user, null);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }
}
