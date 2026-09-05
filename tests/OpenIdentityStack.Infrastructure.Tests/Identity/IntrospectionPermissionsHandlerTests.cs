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
            new Claim("sub", Guid.NewGuid().ToString()), new Claim("client_id", "browser-client"), new Claim("permission", "orders:invoice:read")
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
}
