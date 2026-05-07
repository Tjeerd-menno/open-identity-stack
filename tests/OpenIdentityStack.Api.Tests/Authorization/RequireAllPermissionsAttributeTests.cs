using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Application.Authorization;

namespace OpenIdentityStack.Api.Tests.Authorization;

/// <summary>
/// Tests for satisfying all permission requirements with the policy-based handler.
/// </summary>
public sealed class PermissionAuthorizationHandlerAllTests
{
    private readonly PermissionAuthorizationHandler handler = new();

    [Fact]
    public async Task Succeeds_when_user_has_each_required_permission()
    {
        var requirements = new PermissionRequirement[]
        {
            new PermissionRequirement(Permissions.Users.Read),
            new PermissionRequirement(Permissions.Roles.Write)
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("permission", Permissions.Users.Read),
            new Claim("permission", Permissions.Roles.Write)
        }, "mock"));

        var context = new AuthorizationHandlerContext(requirements, user, resource: null);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Fails_when_user_lacks_one_permission()
    {
        var requirements = new PermissionRequirement[]
        {
            new PermissionRequirement(Permissions.Users.Read),
            new PermissionRequirement(Permissions.Roles.Write)
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("permission", Permissions.Users.Read)
        }, "mock"));

        var context = new AuthorizationHandlerContext(requirements, user, resource: null);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Succeeds_when_scope_claim_covers_all_requirements()
    {
        var requirements = new PermissionRequirement[]
        {
            new PermissionRequirement(Permissions.Users.Read),
            new PermissionRequirement(Permissions.Roles.Read)
        };
        string scopeValue = $"{Permissions.Users.Read} {Permissions.Roles.Read}";
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("scope", scopeValue)
        }, "mock"));

        var context = new AuthorizationHandlerContext(requirements, user, resource: null);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }
}
