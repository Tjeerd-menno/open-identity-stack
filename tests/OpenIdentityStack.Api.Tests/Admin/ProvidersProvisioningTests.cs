using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OpenIdentityStack.Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Infrastructure.Audit;

namespace OpenIdentityStack.Api.Tests.Admin;

public sealed class ProvidersProvisioningTests(AppHostFixture fixture)
{
    [Fact]
    public async Task ProvisioningSetting_PersistsAcrossCreateReadListAndUpdate()
    {
        using HttpClient client = await fixture.CreateAuthenticatedClientAsync(
            $"jit-admin-{Guid.NewGuid():N}", "test-admin-secret");
        HttpResponseMessage created = await client.PostAsJsonAsync("/api/admin/providers", new
        {
            Name = $"jit-{Guid.NewGuid():N}",
            DisplayName = "Provisioning policy",
            Authority = "https://example.com",
            ClientId = "client-id",
            JitProvisioningEnabled = false,
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        JsonNode body = (await created.Content.ReadFromJsonAsync<JsonNode>())!;
        Guid id = body["id"]!.GetValue<Guid>();
        body["jitProvisioningEnabled"]!.GetValue<bool>().ShouldBeFalse();
        await fixture.ExecuteDbContextAsync(async db =>
        {
            AuditLogEntry audit = await db.AuditLogEntries.SingleAsync(entry => entry.EntityId == id.ToString()
                && entry.Action == "Federation.ProviderCreated");
            audit.UserId.ShouldNotBeNullOrWhiteSpace();
            audit.BeforeState.ShouldBeNull();
            audit.AfterState.ShouldBe("{\"jitProvisioningEnabled\":false}");
        });

        JsonNode read = (await client.GetFromJsonAsync<JsonNode>($"/api/admin/providers/{id}"))!;
        read["jitProvisioningEnabled"]!.GetValue<bool>().ShouldBeFalse();
        JsonArray providers = (await client.GetFromJsonAsync<JsonArray>("/api/admin/providers"))!;
        providers.Single(p => p!["id"]!.GetValue<Guid>() == id)!["jitProvisioningEnabled"]!
            .GetValue<bool>().ShouldBeFalse();

        HttpResponseMessage updated = await client.PatchAsJsonAsync($"/api/admin/providers/{id}", new
        {
            JitProvisioningEnabled = true,
        });
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode enabled = (await client.GetFromJsonAsync<JsonNode>($"/api/admin/providers/{id}"))!;
        enabled["jitProvisioningEnabled"]!.GetValue<bool>().ShouldBeTrue();
        await fixture.ExecuteDbContextAsync(async db =>
        {
            AuditLogEntry audit = await db.AuditLogEntries.SingleAsync(entry => entry.EntityId == id.ToString()
                && entry.Action == "Federation.JitProvisioningPolicyChanged");
            audit.UserId.ShouldNotBeNullOrWhiteSpace();
            audit.BeforeState.ShouldBe("{\"jitProvisioningEnabled\":false}");
            audit.AfterState.ShouldBe("{\"jitProvisioningEnabled\":true}");
        });

        await client.PatchAsJsonAsync($"/api/admin/providers/{id}", new { JitProvisioningEnabled = false });
        await client.PatchAsJsonAsync($"/api/admin/providers/{id}", new { DisplayName = "Renamed policy" });
        JsonNode retained = (await client.GetFromJsonAsync<JsonNode>($"/api/admin/providers/{id}"))!;
        retained["jitProvisioningEnabled"]!.GetValue<bool>().ShouldBeFalse();
    }
}

