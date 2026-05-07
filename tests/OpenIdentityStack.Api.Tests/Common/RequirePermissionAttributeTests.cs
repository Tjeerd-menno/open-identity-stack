using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Application.Authorization;

namespace OpenIdentityStack.Api.Tests.Common;

public sealed class PermissionAuthorizationHandlerTests
{
    private readonly PermissionAuthorizationHandler handler = new();

    [Fact]
    public async Task Unauthenticated_user_does_not_succeed()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        AuthorizationHandlerContext context = CreateContext(Permissions.Users.Read, user);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Exact_permission_claim_succeeds()
    {
        var identity = new ClaimsIdentity(new[] { new Claim("permission", Permissions.Users.Read) }, "mock");
        var user = new ClaimsPrincipal(identity);
        AuthorizationHandlerContext context = CreateContext(Permissions.Users.Read, user);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Wildcard_permission_claim_succeeds_for_child_operations()
    {
        var identity = new ClaimsIdentity(new[] { new Claim("permission", Permissions.Users.All) }, "mock");
        var user = new ClaimsPrincipal(identity);
        AuthorizationHandlerContext context = CreateContext(Permissions.Users.Delete, user);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Scope_claim_is_treated_as_permission()
    {
        var identity = new ClaimsIdentity(new[] { new Claim("scope", $"{Permissions.Users.Read} extra") }, "mock");
        var user = new ClaimsPrincipal(identity);
        AuthorizationHandlerContext context = CreateContext(Permissions.Users.Read, user);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Admin_role_grants_all_permissions()
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "admin") }, "mock");
        var user = new ClaimsPrincipal(identity);
        AuthorizationHandlerContext context = CreateContext(Permissions.Roles.Delete, user);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    private static AuthorizationHandlerContext CreateContext(string requiredPermission, ClaimsPrincipal user)
    {
        var requirement = new PermissionRequirement(requiredPermission);
        return new AuthorizationHandlerContext(new[] { requirement }, user, resource: null);
    }
}
