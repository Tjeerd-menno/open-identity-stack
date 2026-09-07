using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Application.Authorization;

namespace OpenIdentityStack.Api.Tests.Authorization;

/// <summary>
/// Tests for permission handler behavior when multiple requirements are present.
/// </summary>
public sealed class PermissionAuthorizationHandlerAnyTests
{
    private readonly PermissionAuthorizationHandler handler = new(new AdministrativeRequestAuthorization(new OpenIdentityStack.Api.Tests.Authorization.ApprovedAdministrativeAccess()));

    [Fact]
    public async Task Multiple_requirements_fail_when_any_requirement_is_unmet()
    {
        var requirements = new PermissionRequirement[]
        {
            new PermissionRequirement(Permissions.Users.Read),
            new PermissionRequirement(Permissions.Roles.Read)
        };
        ClaimsPrincipal user = OpenIdentityStack.Api.Tests.Authorization.ApprovedAdministrativeAccess.Principal(new ClaimsIdentity(new[]
        {
            new Claim("permission", Permissions.Users.Read)
        }, "mock"));

        var context = new AuthorizationHandlerContext(requirements, user, resource: null);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Multiple_requirements_succeed_with_wildcard_permission()
    {
        var requirements = new PermissionRequirement[]
        {
            new PermissionRequirement(Permissions.Users.Read),
            new PermissionRequirement(Permissions.Roles.Read)
        };
        ClaimsPrincipal user = OpenIdentityStack.Api.Tests.Authorization.ApprovedAdministrativeAccess.Principal(new ClaimsIdentity(new[]
        {
            new Claim("permission", Permissions.All)
        }, "mock"));

        var context = new AuthorizationHandlerContext(requirements, user, resource: null);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }
}
