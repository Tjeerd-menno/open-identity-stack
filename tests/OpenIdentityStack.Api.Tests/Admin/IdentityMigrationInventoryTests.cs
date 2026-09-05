using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OpenIdentityStack.Api.Tests.Fixtures;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Api.Tests.Admin;

public sealed class IdentityMigrationInventoryTests(AppHostFixture fixture)
{
    [Fact]
    public async Task Inventory_RetainsQuarantineAndReportsRecoveryWithoutChangingRecords()
    {
        UpstreamProvider provider = UpstreamProvider.Create($"inventory-{Guid.NewGuid():N}", "Inventory", "https://issuer.example", "client").Value;
        User user = User.CreateFederated($"inventory-{Guid.NewGuid():N}@example.com", "Migration user", provider.Id, provider.Name, "legacy-subject", issuer: "https://issuer.example").Value;
        await fixture.ExecuteDbContextAsync(async db =>
        {
            db.UpstreamProviders.Add(provider);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        });
        HttpClient client = await fixture.CreateAuthenticatedClientAsync($"inventory-admin-{Guid.NewGuid():N}", "admin-secret-123!");
        string path = $"/api/admin/users/identity-migration-inventory?providerId={provider.Id.Value}&page=1&pageSize=10";
        HttpResponseMessage response = await client.GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode json = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
        json["totalCount"]!.GetValue<int>().ShouldBe(1);
        JsonNode item = json["items"]![0]!;
        item["userId"]!.GetValue<Guid>().ShouldBe(user.Id.Value);
        item["migrationBlocked"]!.GetValue<bool>().ShouldBeTrue();
        item["recoveryRequired"]!.GetValue<bool>().ShouldBeTrue();
        item["hasPasswordCredential"]!.GetValue<bool>().ShouldBeFalse();
        item["identities"]![0]!["associationEvidence"]!.GetValue<string>().ShouldBe("Unknown");
        item["identities"]![0]!["isQuarantined"]!.GetValue<bool>().ShouldBeTrue();
        HttpResponseMessage unlink = await client.DeleteAsync($"/api/admin/users/{user.Id.Value}/upstream-identities/{provider.Id.Value}");
        unlink.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        HttpResponseMessage toggle = await client.PatchAsJsonAsync($"/api/admin/users/{user.Id.Value}/upstream-identities/{provider.Id.Value}", new { associationEvidence = "NewAccountProvisioning", isQuarantined = false });
        toggle.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        JsonNode userIdentities = (await client.GetFromJsonAsync<JsonNode>($"/api/admin/users/{user.Id.Value}/upstream-identities"))!;
        userIdentities["items"]![0]!["isQuarantined"]!.GetValue<bool>().ShouldBeTrue();
        JsonNode repeated = (await client.GetFromJsonAsync<JsonNode>(path))!;
        repeated.ToJsonString().ShouldBe(json.ToJsonString());
        using HttpClient anonymous = fixture.CreateClient(false);
        (await anonymous.GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
