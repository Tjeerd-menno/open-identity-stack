using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Application.Authorization;

namespace OpenIdentityStack.Api.Tests.Common;

public sealed class PermissionAuthorizationHandlerTests
{
    private readonly PermissionAuthorizationHandler handler = new(new AdministrativeRequestAuthorization(new ApprovedAccessEvaluator()));

    private sealed class ApprovedAccessEvaluator : OpenIdentityStack.Application.Abstractions.IAdministrativeAccessEvaluator
    {
        public Task<SharedKernel.Result<IReadOnlyList<string>>> EvaluateAsync(OpenIdentityStack.Application.Abstractions.AdministrativeAccessRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult((SharedKernel.Result<IReadOnlyList<string>>)request.TokenPermissions.ToList());
    }

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
        var identity = new ClaimsIdentity(AdminClaims(new Claim("permission", Permissions.Users.Read)), "mock");
        var user = new ClaimsPrincipal(identity);
        AuthorizationHandlerContext context = CreateContext(Permissions.Users.Read, user);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Wildcard_permission_claim_succeeds_for_child_operations()
    {
        var identity = new ClaimsIdentity(AdminClaims(new Claim("permission", Permissions.Users.All)), "mock");
        var user = new ClaimsPrincipal(identity);
        AuthorizationHandlerContext context = CreateContext(Permissions.Users.Delete, user);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Scope_claim_does_not_convey_administrative_permission()
    {
        var identity = new ClaimsIdentity(AdminClaims(new Claim("scope", $"{Permissions.Users.Read} extra")), "mock");
        var user = new ClaimsPrincipal(identity);
        AuthorizationHandlerContext context = CreateContext(Permissions.Users.Read, user);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Admin_role_name_does_not_grant_permissions()
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "admin") }, "mock");
        var user = new ClaimsPrincipal(identity);
        AuthorizationHandlerContext context = CreateContext(Permissions.Roles.Delete, user);

        await this.handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://business.example/api")]
    [InlineData("urn:openidentitystack:admin-api https://business.example/api")]
    public async Task Administrative_permission_rejects_missing_wrong_and_combined_audiences(string audiences)
    {
        var claims = new List<Claim> { new("permission", Permissions.Users.Read), new("scope", "ois.admin"), new("client_id", "approved-client"), new("sub", "approved-client") };
        claims.AddRange(audiences.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(value => new Claim("aud", value)));
        AuthorizationHandlerContext context = CreateContext(Permissions.Users.Read, new ClaimsPrincipal(new ClaimsIdentity(claims, "mock")));
        await this.handler.HandleAsync(context);
        context.HasSucceeded.ShouldBeFalse();
    }

    private static Claim[] AdminClaims(Claim permission) => [new("aud", "urn:openidentitystack:admin-api"), new("scope", "ois.admin"), new("client_id", "approved-client"), new("sub", "approved-client"), permission];

    private static AuthorizationHandlerContext CreateContext(string requiredPermission, ClaimsPrincipal user)
    {
        var requirement = new PermissionRequirement(requiredPermission);
        return new AuthorizationHandlerContext(new[] { requirement }, user, resource: null);
    }
}
