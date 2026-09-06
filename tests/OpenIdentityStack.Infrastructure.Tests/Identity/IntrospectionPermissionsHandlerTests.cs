using System.Security.Claims;
using OpenIddict.Abstractions;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Infrastructure.Identity;
using static OpenIddict.Server.OpenIddictServerEvents;
using SharedKernel;
using ClientApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Infrastructure.Tests.Identity;

public sealed class IntrospectionPermissionsHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenTokenHasNoAudience_RejectsWithoutProjectingClaims()
    {
        IResourcePermissionService projection = Substitute.For<IResourcePermissionService>();
        IResourceAccessRepository resources = Substitute.For<IResourceAccessRepository>();
        IApplicationRepository applications = Substitute.For<IApplicationRepository>();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        ClientApplication caller = ClientApplication.CreateMachineToMachine("resource-server", "Resource server", null, ["orders"], clock).Value;
        applications.GetByClientIdAsync(caller.ClientId, Arg.Any<CancellationToken>()).Returns(caller);
        projection.ProjectAsync(Arg.Any<ResourceTokenRequest>(), Arg.Any<CancellationToken>()).Returns(
            (Result<ResourceTokenProjection>)new ResourceTokenProjection([], [], new Dictionary<Guid, long>()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", Guid.NewGuid().ToString()), new Claim("client_id", "browser-client"),
            new Claim(ResourceTokenActorTypes.ClaimType, ResourceTokenActorTypes.User)
        ]));
        principal.SetScopes("openid", "profile", "email");
        var context = new HandleIntrospectionRequestContext(new OpenIddict.Server.OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest { ClientId = caller.ClientId }
        }) { GenericTokenPrincipal = principal };

        await new IntrospectionPermissionsHandler(projection, resources, applications).HandleAsync(context);

        context.IsRejected.ShouldBeTrue();
        await projection.DidNotReceive().ProjectAsync(Arg.Any<ResourceTokenRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_RequiresExplicitResourceAssignmentAndPreservesOriginalTokenCeiling(bool granted)
    {
        IResourcePermissionService projection = Substitute.For<IResourcePermissionService>();
        IResourceAccessRepository resources = Substitute.For<IResourceAccessRepository>();
        IApplicationRepository applications = Substitute.For<IApplicationRepository>();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        ClientApplication caller = ClientApplication.CreateMachineToMachine("unrelated-client", "Caller", null, ["orders"], clock).Value;
        ProtectedResource resource = ProtectedResource.Create("https://orders.example.com", "orders", "Orders", ["orders"]).Value;
        applications.GetByClientIdAsync(caller.ClientId, Arg.Any<CancellationToken>()).Returns(caller);
        resources.FindByAudienceAsync(resource.Audience, Arg.Any<CancellationToken>()).Returns(resource);
        if (granted)
        {
            resources.GetGrantAsync(caller.Id, resource.Id, Arg.Any<CancellationToken>()).Returns(ClientResourceGrant.Create(caller.Id, resource.Id, [], []).Value);
        }
        projection.ProjectAsync(Arg.Any<ResourceTokenRequest>(), Arg.Any<CancellationToken>()).Returns(
            (Result<ResourceTokenProjection>)new ResourceTokenProjection([resource.Audience], ["orders:invoice:read"], new Dictionary<Guid, long>()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", Guid.NewGuid().ToString()), new Claim("client_id", "browser-client"),
            new Claim(ResourceTokenActorTypes.ClaimType, ResourceTokenActorTypes.User), new Claim("permission", "orders:invoice:read")
        ]));
        principal.SetScopes("orders");
        principal.SetAudiences(resource.Audience);
        var context = new HandleIntrospectionRequestContext(new OpenIddict.Server.OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest { ClientId = caller.ClientId }
        }) { GenericTokenPrincipal = principal };
        var handler = new IntrospectionPermissionsHandler(projection, resources, applications);
        await handler.HandleAsync(context);
        context.IsRejected.ShouldBe(!granted);
        if (granted)
        {
            context.Claims["permissions"].GetUnnamedParameters().Select(static value => value.ToString()).ShouldBe(["orders:invoice:read"]);
            await projection.Received().ProjectAsync(Arg.Is<ResourceTokenRequest>(request => request.ClientId == "browser-client"
                && request.OriginalPermissions!.Count == 1 && request.OriginalPermissions[0] == "orders:invoice:read"
                && request.OriginalAudiences!.SequenceEqual(new[] { resource.Audience })), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task HandleAsync_WhenUserIdEqualsClientId_UsesExplicitUserActorType()
    {
        IResourcePermissionService projection = Substitute.For<IResourcePermissionService>();
        IResourceAccessRepository resources = Substitute.For<IResourceAccessRepository>();
        IApplicationRepository applications = Substitute.For<IApplicationRepository>();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        var overlappingId = Guid.NewGuid();
        string clientId = overlappingId.ToString();
        ClientApplication caller = ClientApplication.CreateMachineToMachine("resource-server", "Resource server", null, ["orders"], clock).Value;
        ProtectedResource resource = ProtectedResource.Create("https://orders.example.com", "orders", "Orders", ["orders"]).Value;
        applications.GetByClientIdAsync(caller.ClientId, Arg.Any<CancellationToken>()).Returns(caller);
        resources.FindByAudienceAsync(resource.Audience, Arg.Any<CancellationToken>()).Returns(resource);
        resources.GetGrantAsync(caller.Id, resource.Id, Arg.Any<CancellationToken>()).Returns(ClientResourceGrant.Create(caller.Id, resource.Id, [], []).Value);
        projection.ProjectAsync(Arg.Any<ResourceTokenRequest>(), Arg.Any<CancellationToken>()).Returns(
            (Result<ResourceTokenProjection>)new ResourceTokenProjection([resource.Audience], ["orders:read"], new Dictionary<Guid, long>()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", clientId), new Claim("client_id", clientId),
            new Claim(ResourceTokenActorTypes.ClaimType, ResourceTokenActorTypes.User)
        ]));
        principal.SetScopes("orders");
        principal.SetAudiences(resource.Audience);
        var context = new HandleIntrospectionRequestContext(new OpenIddict.Server.OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest { ClientId = caller.ClientId }
        }) { GenericTokenPrincipal = principal };

        await new IntrospectionPermissionsHandler(projection, resources, applications).HandleAsync(context);

        context.IsRejected.ShouldBeFalse();
        await projection.Received().ProjectAsync(
            Arg.Is<ResourceTokenRequest>(request => request.UserId == new UserId(overlappingId)),
            Arg.Any<CancellationToken>());
    }
}
