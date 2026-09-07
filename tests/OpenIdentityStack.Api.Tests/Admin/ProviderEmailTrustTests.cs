using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Api.Tests.Fixtures;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Admin;

public sealed class ProviderEmailTrustTests(AppHostFixture fixture)
{
    [Fact]
    public async Task TrustPolicyAndEvidenceAreVisibleAndWithdrawalIsAudited()
    {
        using HttpClient client = await fixture.CreateAuthenticatedClientAsync($"email-admin-{Guid.NewGuid():N}", "test-admin-secret");
        HttpResponseMessage created = await client.PostAsJsonAsync("/api/admin/providers", new
        {
            Name = $"email-{Guid.NewGuid():N}", DisplayName = "Email evidence", Authority = "https://issuer.example", ClientId = "client"
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        JsonNode body = (await created.Content.ReadFromJsonAsync<JsonNode>())!;
        Guid id = body["id"]!.GetValue<Guid>();
        body["trustEmailVerification"]!.GetValue<bool>().ShouldBeFalse();
        (await client.PutAsJsonAsync($"/api/admin/providers/{id}/email-verification-trust", new { Trusted = true }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<JsonNode>($"/api/admin/providers/{id}"))!["trustEmailVerification"]!.GetValue<bool>().ShouldBeTrue();
        JsonArray providers = (await client.GetFromJsonAsync<JsonArray>("/api/admin/providers"))!;
        providers.Single(p => p!["id"]!.GetValue<Guid>() == id)!["trustEmailVerification"]!.GetValue<bool>().ShouldBeTrue();
        (await client.PatchAsJsonAsync($"/api/admin/providers/{id}", new { DisplayName = "Updated" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<JsonNode>($"/api/admin/providers/{id}"))!["trustEmailVerification"]!.GetValue<bool>().ShouldBeTrue();

        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        User user = User.CreateFederated($"{Guid.NewGuid():N}@example.com", "Evidence subject", clock).Value;
        await fixture.ExecuteDbContextAsync(async db =>
        {
            UpstreamProvider provider = await db.UpstreamProviders.SingleAsync(p => p.Id == new UpstreamProviderId(id));
            user.RecordProviderEmailVerification(provider, "https://issuer.example", user.Email, true, clock.UtcNow);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        });
        JsonNode verified = (await client.GetFromJsonAsync<JsonNode>($"/api/admin/users/{user.Id.Value}"))!;
        verified["emailVerified"]!.GetValue<bool>().ShouldBeTrue();
        verified["emailVerificationEvidence"]![0]!["providerId"]!.GetValue<Guid>().ShouldBe(id);
        verified["emailVerificationEvidence"]![0]!["issuer"]!.GetValue<string>().ShouldBe("https://issuer.example");

        (await client.PutAsJsonAsync($"/api/admin/providers/{id}/email-verification-trust", new { Trusted = false }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        JsonNode withdrawn = (await client.GetFromJsonAsync<JsonNode>($"/api/admin/users/{user.Id.Value}"))!;
        withdrawn["emailVerified"]!.GetValue<bool>().ShouldBeFalse();
        withdrawn["emailVerificationEvidence"]![0]!["withdrawnAt"].ShouldNotBeNull();
        await fixture.ExecuteDbContextAsync(async db =>
            (await db.AuditLogEntries.CountAsync(e => e.Action == "Provider.EmailVerificationTrustChanged" && e.EntityId == id.ToString())).ShouldBe(2));
    }
}
