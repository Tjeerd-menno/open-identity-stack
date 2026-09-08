using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Api.Tests.Fixtures;
using OpenIdentityStack.Application.AdministrativeAccess;
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Domain.Roles;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Admin;

public sealed class AdministrativeEntitlementTests(AppHostFixture fixture)
{
    [Fact]
    public async Task AuthenticationChallengeRemainsBodyless()
    {
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType.ShouldBeNull();
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("oauth")]
    [InlineData("enable")]
    [InlineData("credentials/client-secrets")]
    [InlineData("credentials/certificates")]
    public async Task MachineCannotTransferApprovedClientThroughOrdinaryApplicationChanges(string operation)
    {
        using HttpClient actor = await fixture.CreateAuthenticatedClientAsync($"client-editor-{Guid.NewGuid():N}", "fixture-secret");
        Guid targetId = await this.CreateTargetAsync();
        await fixture.ExecuteDbContextAsync(async db =>
        {
            OpenIdentityStack.Domain.Applications.Application application = await db.Applications.SingleAsync(client => client.Id == new OpenIdentityStack.Domain.Applications.ApplicationId(targetId));
            db.Set<ClientResourceGrant>().Add(ClientResourceGrant.Create(application.Id, ProtectedResource.AdministrativeResourceId, [], ["users:read"]).Value);
            if (operation == "enable")
            {
                IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
                clock.UtcNow.Returns(DateTimeOffset.UtcNow);
                application.Disable(clock);
            }
            await db.SaveChangesAsync();
        });
        using HttpResponseMessage response = operation == "oauth"
            ? await actor.PutAsJsonAsync($"/api/admin/applications/{targetId}/oauth", new
            {
                Profile = "MachineToMachine", ClientType = "Confidential", AllowedGrantTypes = (string[])["client_credentials"],
                AllowedScopes = (string[])["ois.admin"], RedirectUris = Array.Empty<string>(), PostLogoutRedirectUris = Array.Empty<string>(),
                RequirePkce = false, RequireConsent = false,
            })
            : await actor.PostAsJsonAsync($"/api/admin/applications/{targetId}/{operation}", new { RevokeExisting = false, Thumbprint = "ABCDEF1234" });
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).ShouldContain("AdministrativeApproval.HumanRequired");
    }

    [Fact]
    public async Task UnapprovedClientCannotObtainAdministrativeToken()
    {
        string clientId = $"unapproved-{Guid.NewGuid():N}";
        await fixture.CreateServiceAccountAsync(clientId, "fixture-secret", ["ois.admin"]);
        using HttpClient client = fixture.CreateClient();
        using HttpResponseMessage response = await client.PostAsync("/connect/token", TokenRequest(clientId));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ApprovedMachineIsCeilingLimitedAndWithdrawalAffectsExistingToken()
    {
        string clientId = $"limited-{Guid.NewGuid():N}";
        using HttpClient client = await fixture.CreateAuthenticatedClientAsync(
            clientId,
            "fixture-secret",
            administrativePermissions: ["users:read"]);
        (await client.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/admin/roles")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        JsonNode currentUser = (await client.GetFromJsonAsync<JsonNode>("/api/me"))!;
        currentUser["permissions"]!.AsArray().Select(value => value!.GetValue<string>()).ShouldBe(["users:read"]);
        await this.SetCeilingAsync(clientId, [], []);
        (await client.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MachineCannotApproveClientOrBypassThroughGenericResourceGrant()
    {
        using HttpClient actor = await fixture.CreateAuthenticatedClientAsync($"approver-machine-{Guid.NewGuid():N}", "fixture-secret");
        Guid targetId = await this.CreateTargetAsync();
        using HttpResponseMessage approval = await actor.PutAsJsonAsync($"/api/admin/applications/{targetId}/administrative-access",
            new AdministrativeAccessConfiguration([], ["users:read"], null, true));
        approval.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await approval.Content.ReadAsStringAsync()).ShouldContain("AdministrativeApproval.HumanRequired");
        using HttpResponseMessage generic = await actor.PutAsJsonAsync($"/api/admin/applications/{targetId}/resource-grants/{ProtectedResource.AdministrativeResourceId}",
            new { DelegatedPermissions = (string[])["*"], ApplicationPermissions = Array.Empty<string>() });
        generic.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OrdinaryAdministrativeAccessDenialReturnsProblemDetailsWithoutApprovalCode()
    {
        using HttpClient actor = await fixture.CreateAuthenticatedClientAsync($"ordinary-denial-{Guid.NewGuid():N}", "fixture-secret");
        using HttpResponseMessage response = await actor.PutAsJsonAsync(
            $"/api/admin/applications/{Guid.NewGuid()}/administrative-access",
            new AdministrativeAccessConfiguration([], ["users:read"], null, true));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        JsonNode problem = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
        problem["status"]!.GetValue<int>().ShouldBe(403);
        problem["errorCode"].ShouldBeNull();
    }

    [Fact]
    public async Task FreshHumanApprovesReviewedClientAndRefreshRemainsWithinReducedCeiling()
    {
        string email = $"human-approval-{Guid.NewGuid():N}@example.com";
        const string password = "Password123!@#";
        Guid userId = await fixture.CreateTestUserAsync(email, "Approver", password);
        await fixture.ExecuteDbContextAsync(async db =>
        {
            Role role = Role.Create($"approver-{Guid.NewGuid():N}", "Approver", null).Value;
            role.SetPermissions(["*"]);
            db.Roles.Add(role);
            db.RoleAssignments.Add(RoleAssignment.Create(new UserId(userId), role.Id, DateTimeOffset.UtcNow).Value);
            await db.SaveChangesAsync();
        });
        HumanAdministrativeSession session = await HumanAdministrativeSession.SignInAsync(fixture, email, password, ["*"]);
        using HttpClient actor = session.Client;
        Guid targetId = await this.CreateTargetAsync();
        var request = new AdministrativeAccessConfiguration([], ["users:read"], null);
        using HttpResponseMessage unacknowledged = await actor.PutAsJsonAsync($"/api/admin/applications/{targetId}/administrative-access", request);
        unacknowledged.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await unacknowledged.Content.ReadAsStringAsync()).ShouldContain("AdministrativeApproval.AcknowledgementRequired");
        actor.DefaultRequestHeaders.Add("X-OIS-Administrative-Approval", "acknowledge");
        using HttpResponseMessage approved = await actor.PutAsJsonAsync($"/api/admin/applications/{targetId}/administrative-access", request);
        approved.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonObject contract = (await approved.Content.ReadFromJsonAsync<JsonObject>())!;
        contract.Select(property => property.Key).Order(StringComparer.Ordinal).ShouldBe(["applicationPermissions", "approved", "delegatedPermissions", "revision"]);
        contract["approved"]!.GetValue<bool>().ShouldBeTrue();
        contract["applicationPermissions"]!.AsArray().Select(value => value!.GetValue<string>()).ShouldBe(["users:read"]);
        contract["delegatedPermissions"]!.AsArray().ShouldBeEmpty();

        await this.SetCeilingAsync(session.ClientId, ["users:read"], []);
        using HttpResponseMessage refresh = await actor.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["refresh_token"] = session.RefreshToken,
            ["client_id"] = session.ClientId, ["client_secret"] = session.ClientSecret,
        }));
        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode tokens = (await refresh.Content.ReadFromJsonAsync<JsonNode>())!;
        actor.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens["access_token"]!.GetValue<string>());
        (await actor.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await actor.GetAsync("/api/admin/roles")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task SetCeilingAsync(string clientId, IReadOnlyList<string> delegated, IReadOnlyList<string> machine)
    {
        await fixture.ExecuteDbContextAsync(async db =>
        {
            OpenIdentityStack.Domain.Applications.Application application = await db.Applications.SingleAsync(client => client.ClientId == clientId);
            ClientResourceGrant grant = await db.Set<ClientResourceGrant>().SingleAsync(grant => grant.ClientApplicationId == application.Id && grant.ResourceId == ProtectedResource.AdministrativeResourceId);
            grant.Configure(delegated, machine);
            await db.SaveChangesAsync();
        });
    }

    private async Task<Guid> CreateTargetAsync()
    {
        string clientId = $"admin-target-{Guid.NewGuid():N}";
        await fixture.CreateServiceAccountAsync(clientId, "fixture-secret", ["ois.admin"]);
        Guid id = Guid.Empty;
        await fixture.ExecuteDbContextAsync(async db => id = (await db.Applications.SingleAsync(client => client.ClientId == clientId)).Id.Value);
        return id;
    }

    private static FormUrlEncodedContent TokenRequest(string clientId) => new(new Dictionary<string, string>
    {
        ["grant_type"] = "client_credentials", ["client_id"] = clientId, ["client_secret"] = "fixture-secret", ["scope"] = "ois.admin",
    });
}
