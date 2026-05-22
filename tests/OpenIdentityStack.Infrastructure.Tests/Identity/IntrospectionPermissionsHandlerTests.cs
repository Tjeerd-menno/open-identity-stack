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
            Permissions: ["patient-api:read-patients", "patient-api:write-patients", "inventory-api:read-stock"]);

        this.getUserEffectiveRolesQueryHandler.HandleAsync(
                Arg.Is<UserId>(id => id.Value == userId),
                Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)new[] { role });

        // Act
        await handler.HandleAsync(context);

        // Assert
        GetPermissions(context).ShouldBe(["patient-api:read-patients", "patient-api:write-patients"]);
    }

    [Fact]
    public async Task HandleAsync_FallsBackToTokenPermissionClaimsForNonUserSubjects()
    {
        // Arrange
        var handler = new IntrospectionPermissionsHandler(this.getUserEffectiveRolesQueryHandler);
        HandleIntrospectionRequestContext context = CreateContext("patient-api", "service-account");
        context.GenericTokenPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("permission", "patient-api:read-patients inventory-api:read-stock"),
            new Claim("permissions", "patient-api:write-patients")
        ]));

        // Act
        await handler.HandleAsync(context);

        // Assert
        GetPermissions(context).ShouldBe(["patient-api:read-patients", "patient-api:write-patients"]);
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
