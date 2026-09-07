using OpenIdentityStack.Domain.Common;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Roles;
using SharedKernel;
using OpenIdentityStack.Api.Tests.Fixtures;

namespace OpenIdentityStack.Api.Tests.Admin;

public sealed class UnrestrictedGrantTests(AppHostFixture fixture)
{
    [Theory]
    [InlineData("enable")]
    [InlineData("reset-password")]
    public async Task MachineCannotRestoreOrTakeOverUnrestrictedUser(string operation)
    {
        Guid userId = await fixture.CreateTestUserAsync($"protected-{Guid.NewGuid():N}@example.com", "Protected Operator", "Password123!@#");
        await fixture.ExecuteDbContextAsync(async db =>
        {
            Role role = Role.Create($"protected-{Guid.NewGuid():N}", "Protected", null).Value;
            role.SetPermissions(["*"]);
            db.Roles.Add(role);
            db.RoleAssignments.Add(RoleAssignment.Create(new UserId(userId), role.Id, DateTimeOffset.UtcNow).Value);
            await db.SaveChangesAsync();
        });
        if (operation == "enable") { await fixture.DisableUserAsync(userId); }
        using HttpClient client = await fixture.CreateAuthenticatedClientAsync($"restore-{Guid.NewGuid():N}", "test-admin-secret");
        HttpResponseMessage result = await client.PostAsJsonAsync($"/api/admin/users/{userId}/{operation}", new { NewPassword = "DifferentPassword123!@#" });
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }


    [Fact]
    public async Task FreshHumanCanApproveAndPersistedWithdrawalRejectsExistingToken()
    {
        string email = $"approval-{Guid.NewGuid():N}@example.com";
        const string password = "Password123!@#";
        Guid userId = await fixture.CreateTestUserAsync(email, "Approval Operator", password);
        Guid roleId = await fixture.CreateTestRoleAsync($"approval-role-{Guid.NewGuid():N}");
        await fixture.ExecuteDbContextAsync(async db =>
        {
            Role role = (await db.Roles.FirstAsync(role => role.Id == new RoleId(roleId)))!;
            role.SetPermissions(["*"]);
            await db.SaveChangesAsync();
        });
        await fixture.ExecuteDbContextAsync(async db =>
        {
            db.RoleAssignments.Add(RoleAssignment.Create(new UserId(userId), new RoleId(roleId), DateTimeOffset.UtcNow).Value);
            await db.SaveChangesAsync();
        });
        using HttpClient client = await this.SignInHumanAsync(email, password);
        Guid protectedId = await fixture.CreateTestUserAsync($"restored-{Guid.NewGuid():N}@example.com", "Protected Operator", password);
        await fixture.ExecuteDbContextAsync(async db =>
        {
            db.RoleAssignments.Add(RoleAssignment.Create(new UserId(protectedId), new RoleId(roleId), DateTimeOffset.UtcNow).Value);
            await db.SaveChangesAsync();
        });
        await fixture.DisableUserAsync(protectedId);
        HttpResponseMessage unacknowledged = await client.PostAsJsonAsync($"/api/admin/users/{protectedId}/enable", new { });
        unacknowledged.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await unacknowledged.Content.ReadAsStringAsync()).ShouldContain("AdministrativeApproval.AcknowledgementRequired");
        client.DefaultRequestHeaders.Add("X-OIS-Administrative-Approval", "acknowledge");
        HttpResponseMessage enabled = await client.PostAsJsonAsync($"/api/admin/users/{protectedId}/enable", new { });
        enabled.StatusCode.ShouldBe(HttpStatusCode.OK);
        HttpResponseMessage reset = await client.PostAsJsonAsync($"/api/admin/users/{protectedId}/reset-password", new { NewPassword = "DifferentPassword123!@#" });
        reset.StatusCode.ShouldBe(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Remove("X-OIS-Administrative-Approval");
        var request = new { Name = $"approved-{Guid.NewGuid():N}", Permissions = (string[])["*"], AcknowledgeWildcardGrant = true };
        HttpResponseMessage granted = await client.PostAsJsonAsync("/api/admin/roles", request);
        granted.StatusCode.ShouldBe(HttpStatusCode.Created);
        await fixture.ExecuteDbContextAsync(async db =>
        {
            (await db.AuditLogEntries.AnyAsync(entry => entry.UserId == userId.ToString() &&
                entry.Action == "AdministrativeApproval.MutationSucceeded")).ShouldBeTrue();
            Role role = await db.Roles.FirstAsync(role => role.Id == new RoleId(roleId));
            role.SetPermissions(["roles:write"]);
            await db.SaveChangesAsync();
        });
        HttpResponseMessage denied = await client.PostAsJsonAsync("/api/admin/roles",
            new { Name = $"withdrawn-{Guid.NewGuid():N}", Permissions = (string[])["*"], AcknowledgeWildcardGrant = true });
        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        string denial = await denied.Content.ReadAsStringAsync();
        denial.ShouldContain("AdministrativeApproval.AuthorityRequired");
    }

    private async Task<HttpClient> SignInHumanAsync(string email, string password)
    {
        string clientId = $"human-{Guid.NewGuid():N}";
        const string clientSecret = "test-client-secret";
        const string redirectUri = "https://localhost/callback";
        await fixture.CreateServiceAccountAsync(clientId, clientSecret, ["openid", "api"],
            ["authorization_code"], [redirectUri]);
        string verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        string challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        string query = await new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["response_type"] = "code", ["client_id"] = clientId, ["redirect_uri"] = redirectUri,
            ["scope"] = "openid api", ["code_challenge"] = challenge, ["code_challenge_method"] = "S256",
        }).ReadAsStringAsync();
        HttpClient client = fixture.CreateClient(allowAutoRedirect: false);
        HttpResponseMessage page = await client.GetAsync("/Account/Login");
        string html = await page.Content.ReadAsStringAsync();
        Match match = Regex.Match(html, "<input[^>]+name=\"__RequestVerificationToken\"[^>]+value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
        match.Success.ShouldBeTrue();
        HttpResponseMessage login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = password, ["returnUrl"] = "/connect/authorize?" + query,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups["value"].Value),
        }));
        login.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        HttpResponseMessage authorize = await client.GetAsync(login.Headers.Location);
        authorize.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        string code = QueryHelpers.ParseQuery(authorize.Headers.Location!.Query)["code"].Single()!;
        HttpResponseMessage tokenResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = redirectUri,
            ["code_verifier"] = verifier, ["client_id"] = clientId, ["client_secret"] = clientSecret,
        }));
        tokenResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode tokens = (await tokenResponse.Content.ReadFromJsonAsync<JsonNode>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens["access_token"]!.GetValue<string>());
        return client;
    }

    [Fact]
    public async Task MachineCannotCreateUnrestrictedRoleEvenWhenAcknowledged()
    {
        using HttpClient client = await fixture.CreateAuthenticatedClientAsync(
            $"unrestricted-{Guid.NewGuid():N}", "test-admin-secret");
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/admin/roles", new
        {
            Name = $"unsafe-{Guid.NewGuid():N}",
            DisplayName = "Unrestricted role",
            Permissions = (string[])["*"],
            AcknowledgeWildcardGrant = true,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
