using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Api.Tests.Fixtures;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Domain.ApplicationPermissions;
using OpenIdentityStack.Domain.Resources;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Admin;

public sealed class ResourceAccessTests(AppHostFixture fixture)
{
    [Fact]
    public async Task ResourcesAndGrants_HttpWorkflowPersistsExactMachinePermissionsAndRejectsUnknownResource()
    {
        string suffix = Guid.NewGuid().ToString("N")[..12];
        string permissionNamespace = "orders-" + suffix;
        string clientId = "business-" + suffix;
        string resourceScope = "resource-" + suffix;
        string audience = "https://orders.example.com/" + suffix;
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        await fixture.ExecuteDbContextAsync(async db =>
        {
            db.RegisteredApplications.Add(RegisteredApplication.Register(permissionNamespace, "Orders", null, "operator", OwnerType.User,
                [("invoice:read", "Read", null, null), ("invoice:write", "Write", null, null)], "operator", clock).Value);
            await db.SaveChangesAsync();
        });
        using HttpClient admin = await fixture.CreateAuthenticatedClientAsync("resource-admin-" + suffix, "fixture-secret");
        using HttpResponseMessage create = await admin.PostAsJsonAsync("/api/admin/applications/resources",
            new ResourceConfiguration(audience, resourceScope, "Orders", [permissionNamespace]));
        create.StatusCode.ShouldBe(HttpStatusCode.OK);
        ProtectedResourceDto resource = (await create.Content.ReadFromJsonAsync<ProtectedResourceDto>())!;
        resource.Audience.ShouldBe(audience);
        resource.Revision.ShouldBeGreaterThan(0);
        await fixture.CreateServiceAccountAsync(clientId, "fixture-secret", [resourceScope]);
        Guid applicationId = Guid.Empty;
        await fixture.ExecuteDbContextAsync(async db => applicationId = (await db.Applications.SingleAsync(application => application.ClientId == clientId)).Id.Value);

        using HttpResponseMessage denied = await fixture.CreateClient().PostAsync("/connect/token", TokenRequest(clientId, resourceScope));
        denied.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using HttpResponseMessage grant = await admin.PutAsJsonAsync($"/api/admin/applications/{applicationId}/resource-grants/{resource.Id}",
            new ClientResourceGrantConfiguration([$"{permissionNamespace}:invoice:write"], [$"{permissionNamespace}:invoice:read"]));
        grant.StatusCode.ShouldBe(HttpStatusCode.OK);
        ClientResourceGrantDto saved = (await grant.Content.ReadFromJsonAsync<ClientResourceGrantDto>())!;
        saved.ApplicationPermissions.ShouldBe([$"{permissionNamespace}:invoice:read"]);
        string token = await fixture.GetAccessTokenAsync(clientId, "fixture-secret", resourceScope);
        using HttpResponseMessage wrongResource = await fixture.CreateClient().PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials", ["client_id"] = clientId, ["client_secret"] = "fixture-secret", ["scope"] = resourceScope,
            ["resource"] = "https://unrelated.example.com"
        }));
        wrongResource.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await wrongResource.Content.ReadFromJsonAsync<JsonObject>())!["error"]!.GetValue<string>().ShouldBe("invalid_target");
        using HttpResponseMessage introspection = await fixture.CreateClient().PostAsync("/connect/introspect", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId, ["client_secret"] = "fixture-secret", ["token"] = token
        }));
        introspection.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonObject body = (await introspection.Content.ReadFromJsonAsync<JsonObject>())!;
        body["active"]!.GetValue<bool>().ShouldBeTrue();
        body["permissions"]!.AsArray().Select(static value => value!.GetValue<string>()).ShouldBe([$"{permissionNamespace}:invoice:read"]);
        using HttpResponseMessage stale = await admin.PutAsJsonAsync($"/api/admin/applications/{applicationId}/resource-grants/{resource.Id}",
            new ClientResourceGrantConfiguration([], [], saved.Revision + 1));
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using HttpResponseMessage narrowed = await admin.PutAsJsonAsync($"/api/admin/applications/{applicationId}/resource-grants/{resource.Id}",
            new ClientResourceGrantConfiguration([], [], saved.Revision));
        narrowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        using HttpResponseMessage current = await fixture.CreateClient().PostAsync("/connect/introspect", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId, ["client_secret"] = "fixture-secret", ["token"] = token
        }));
        JsonObject currentBody = (await current.Content.ReadFromJsonAsync<JsonObject>())!;
        currentBody["active"]!.GetValue<bool>().ShouldBeTrue();
        (currentBody["permissions"]?.AsArray().Count ?? 0).ShouldBe(0);
    }

    [Fact]
    public async Task GenericApplicationApi_RejectsReservedAdminGrantAndAnonymousResourceRead()
    {
        using HttpClient anonymous = fixture.CreateClient();
        using HttpResponseMessage denied = await anonymous.GetAsync("/api/admin/applications/resources");
        denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        string clientId = "resource-reserved-" + Guid.NewGuid().ToString("N");
        using HttpClient admin = await fixture.CreateAuthenticatedClientAsync(clientId, "fixture-secret");
        Guid applicationId = Guid.Empty;
        await fixture.ExecuteDbContextAsync(async db => applicationId = (await db.Applications.SingleAsync(application => application.ClientId == clientId)).Id.Value);
        using HttpResponseMessage rejected = await admin.PutAsJsonAsync($"/api/admin/applications/{applicationId}/resource-grants/{ProtectedResource.AdministrativeResourceId}",
            new ClientResourceGrantConfiguration(["*"], ["*"]));
        rejected.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        JsonObject problem = (await rejected.Content.ReadFromJsonAsync<JsonObject>())!;
        problem["status"]!.GetValue<int>().ShouldBe(403);
    }

    private static FormUrlEncodedContent TokenRequest(string clientId, string scope) => new(new Dictionary<string, string>
    {
        ["grant_type"] = "client_credentials", ["client_id"] = clientId, ["client_secret"] = "fixture-secret", ["scope"] = scope
    });
}
