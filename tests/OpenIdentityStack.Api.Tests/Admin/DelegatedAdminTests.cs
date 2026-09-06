using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Application.Authorization;

namespace OpenIdentityStack.Api.Tests.Admin;

/// <summary>
/// Authorization checks for delegated admin scenarios using policy-based permissions.
/// </summary>
public class DelegatedAdminTests
{
    private readonly PermissionAuthorizationHandler handler = new();

    [Fact]
    public async Task Delegated_admin_with_user_read_cannot_read_roles()
    {
        Claim[] claims = new[] { new Claim("permission", Permissions.Users.Read) };
        AuthorizationHandlerContext usersContext = CreateContext(Permissions.Users.Read, claims);
        AuthorizationHandlerContext rolesContext = CreateContext(Permissions.Roles.Read, claims);

        await this.handler.HandleAsync(usersContext);
        await this.handler.HandleAsync(rolesContext);

        usersContext.HasSucceeded.ShouldBeTrue();
        rolesContext.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Wildcard_resource_permission_allows_related_operations()
    {
        Claim[] claims = new[] { new Claim("permission", Permissions.Users.All) };
        AuthorizationHandlerContext readContext = CreateContext(Permissions.Users.Read, claims);
        AuthorizationHandlerContext writeContext = CreateContext(Permissions.Users.Write, claims);
        AuthorizationHandlerContext deleteContext = CreateContext(Permissions.Users.Delete, claims);

        await this.handler.HandleAsync(readContext);
        await this.handler.HandleAsync(writeContext);
        await this.handler.HandleAsync(deleteContext);

        readContext.HasSucceeded.ShouldBeTrue();
        writeContext.HasSucceeded.ShouldBeTrue();
        deleteContext.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Admin_role_claim_conveys_no_permissions()
    {
        Claim[] claims = new[] { new Claim(ClaimTypes.Role, "admin") };
        AuthorizationHandlerContext usersContext = CreateContext(Permissions.Users.Delete, claims);
        AuthorizationHandlerContext rolesContext = CreateContext(Permissions.Roles.Delete, claims);
        AuthorizationHandlerContext groupsContext = CreateContext(Permissions.Groups.Delete, claims);

        await this.handler.HandleAsync(usersContext);
        await this.handler.HandleAsync(rolesContext);
        await this.handler.HandleAsync(groupsContext);

        usersContext.HasSucceeded.ShouldBeFalse();
        rolesContext.HasSucceeded.ShouldBeFalse();
        groupsContext.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Global_wildcard_matches_everything()
    {
        Claim[] claims = new[] { new Claim("permission", Permissions.All) };
        AuthorizationHandlerContext usersContext = CreateContext(Permissions.Users.Read, claims);
        AuthorizationHandlerContext rolesContext = CreateContext(Permissions.Roles.Write, claims);
        AuthorizationHandlerContext groupsContext = CreateContext(Permissions.Groups.Delete, claims);

        await this.handler.HandleAsync(usersContext);
        await this.handler.HandleAsync(rolesContext);
        await this.handler.HandleAsync(groupsContext);

        usersContext.HasSucceeded.ShouldBeTrue();
        rolesContext.HasSucceeded.ShouldBeTrue();
        groupsContext.HasSucceeded.ShouldBeTrue();
    }

    private static AuthorizationHandlerContext CreateContext(string requiredPermission, Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "mock");
        var user = new ClaimsPrincipal(identity);
        var requirement = new PermissionRequirement(requiredPermission);
        return new AuthorizationHandlerContext(new[] { requirement }, user, resource: null);
    }
}
