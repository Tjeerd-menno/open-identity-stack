using System.Security.Claims;

using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Infrastructure.Identity;
using static OpenIddict.Server.OpenIddictServerEvents;

using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Identity;

public sealed class IntrospectionPermissionsHandlerTests
{
    private readonly IGetUserEffectiveRolesQueryHandler getUserEffectiveRolesQueryHandler = Substitute.For<IGetUserEffectiveRolesQueryHandler>();

    [Fact]
    public async Task HandleAsync_UsesFreshRolePermissionsAndFiltersByRequestingClient()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var handler = new IntrospectionPermissionsHandler(this.getUserEffectiveRolesQueryHandler);
        HandleIntrospectionRequestContext context = CreateContext("patient-api", userId.ToString());

        var role = new RoleDto(
            Guid.NewGuid(),
            "patient-user",
            "Patient User",
            "Patient permissions",
            IsSystemRole: false,
            IsActive: true,
            Permissions: ["patient-api:patient:read", "patient-api:patient:write", "inventory-api:stock:read"]);

        this.getUserEffectiveRolesQueryHandler.HandleAsync(
                Arg.Is<UserId>(id => id.Value == userId),
                Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)new[] { role });

        // Act
        await handler.HandleAsync(context);

        // Assert
        GetPermissions(context).ShouldBe(["patient-api:patient:read", "patient-api:patient:write"]);
    }

    [Fact]
    public async Task HandleAsync_EmitsConcreteDynamicPermissionsOnlyAndOmitsPlatformAndWildcardPermissions()
    {
        var userId = Guid.NewGuid();
        var handler = new IntrospectionPermissionsHandler(this.getUserEffectiveRolesQueryHandler);
        HandleIntrospectionRequestContext context = CreateContext("patient-api", userId.ToString());

        var role = new RoleDto(
            Guid.NewGuid(),
            "patient-admin",
            "Patient Admin",
            "Patient permissions",
            IsSystemRole: false,
            IsActive: true,
            Permissions: ["*", "users:read", "patient-api:patient:*", "patient-api:patient:read"]);

        this.getUserEffectiveRolesQueryHandler.HandleAsync(
                Arg.Is<UserId>(id => id.Value == userId),
                Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)new[] { role });

        await handler.HandleAsync(context);

        GetPermissions(context).ShouldBe(["patient-api:patient:read"]);
    }

    [Fact]
    public async Task HandleAsync_FallsBackToTokenPermissionClaimsForNonUserSubjects()
    {
        // Arrange
        var handler = new IntrospectionPermissionsHandler(this.getUserEffectiveRolesQueryHandler);
        HandleIntrospectionRequestContext context = CreateContext("patient-api", "service-account");
        context.GenericTokenPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("permission", "patient-api:patient:read inventory-api:stock:read"),
            new Claim("permissions", "patient-api:patient:write")
        ]));

        // Act
        await handler.HandleAsync(context);

        // Assert
        GetPermissions(context).ShouldBe(["patient-api:patient:read", "patient-api:patient:write"]);
        await this.getUserEffectiveRolesQueryHandler.DidNotReceive().HandleAsync(
            Arg.Any<UserId>(),
            Arg.Any<CancellationToken>());
    }

    private static HandleIntrospectionRequestContext CreateContext(string requestingClientId, string subject)
    {
        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest
            {
                ClientId = requestingClientId
            }
        };

        var context = new HandleIntrospectionRequestContext(transaction)
        {
            Subject = subject,
            GenericTokenPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(OpenIddictConstants.Claims.Subject, subject),
                new Claim("permission", "patient-api:stale-token-permission")
            ]))
        };

        return context;
    }

    private static IReadOnlyList<string?> GetPermissions(HandleIntrospectionRequestContext context) =>
        context.Claims["permissions"]
            .GetUnnamedParameters()
            .Select(static parameter => parameter.ToString())
            .ToList();
}
