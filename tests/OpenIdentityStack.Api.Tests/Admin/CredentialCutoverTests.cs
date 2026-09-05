using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Api.Tests.Fixtures;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Infrastructure.Persistence;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Admin;

public sealed class CredentialCutoverTests
{
    [Fact]
    public async Task RemovedSessionRejectsItsAlreadyIssuedRefreshToken()
    {
        await using var fixture = new AppHostFixture($"removed-session-{Guid.NewGuid():N}");
        await fixture.InitializeAsync();
        string email = $"session-{Guid.NewGuid():N}@example.test";
        const string password = "Password123!@#";
        Guid userId = await fixture.CreateTestUserAsync(email, "Session test", password);
        await fixture.ExecuteDbContextAsync(async db =>
        {
            Role role = Role.Create("session-reader", null).Value;
            role.SetPermissions(["users:read"]);
            db.Roles.Add(role);
            db.RoleAssignments.Add(RoleAssignment.Create(new UserId(userId), role.Id, DateTimeOffset.UtcNow).Value);
            await db.SaveChangesAsync();
        });
        HumanAdministrativeSession session = await HumanAdministrativeSession.SignInAsync(fixture, email, password, ["users:read"]);
        using HttpClient client = session.Client;
        await fixture.ExecuteDbContextAsync(async db =>
        {
            db.UserSessions.RemoveRange(await db.UserSessions.Where(candidate => candidate.UserId == new UserId(userId)).ToListAsync());
            await db.SaveChangesAsync();
        });
        HttpResponseMessage response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["refresh_token"] = session.RefreshToken,
            ["client_id"] = session.ClientId, ["client_secret"] = session.ClientSecret
        }));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CutoverRequiresHumanApprovalRejectsOldCredentialsAndFreshLoginRecovers()
    {
        await using var fixture = new AppHostFixture($"cutover-{Guid.NewGuid():N}");
        await fixture.InitializeAsync();
        string email = $"emergency-{Guid.NewGuid():N}@example.com";
        const string password = "Password123!@#";
        Guid userId = await fixture.CreateTestUserAsync(email, "Emergency Operator", password);
        await fixture.ExecuteDbContextAsync(async db =>
        {
            Role role = Role.Create("emergency", "Emergency", null).Value;
            role.SetPermissions(["*"]);
            db.Roles.Add(role);
            db.RoleAssignments.Add(RoleAssignment.Create(new UserId(userId), role.Id, DateTimeOffset.UtcNow).Value);
            await db.SaveChangesAsync();
        });
        using HttpClient machine = await fixture.CreateAuthenticatedClientAsync("cutover-machine", "machine-secret");
        var operation = Guid.NewGuid();
        (await machine.PostAsJsonAsync("/api/admin/security/credential-cutovers", new { OperationId = operation })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var helper = new UnrestrictedGrantTests(fixture);
        using HttpClient human = await helper.SignInHumanAsync(email, password);
        const string grantClient = "cutover-grants";
        const string grantSecret = "grant-secret";
        const string redirectUri = "https://localhost/callback";
        await fixture.CreateServiceAccountAsync(grantClient, grantSecret, ["openid", "offline_access"], ["authorization_code", "refresh_token"], [redirectUri]);
        string verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        string challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        string authorizeUrl = $"/connect/authorize?client_id={grantClient}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=openid%20offline_access&code_challenge={challenge}&code_challenge_method=S256";
        async Task<string> AuthorizeAsync()
        {
            HttpResponseMessage response = await human.GetAsync(authorizeUrl);
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            return QueryHelpers.ParseQuery(response.Headers.Location!.Query)["code"].Single()!;
        }
        Dictionary<string, string> ExchangeCode(string code) => new()
        {
            ["grant_type"] = "authorization_code", ["client_id"] = grantClient, ["client_secret"] = grantSecret,
            ["redirect_uri"] = redirectUri, ["code_verifier"] = verifier, ["code"] = code
        };
        HttpResponseMessage tokenResponse = await human.PostAsync("/connect/token", new FormUrlEncodedContent(ExchangeCode(await AuthorizeAsync())));
        tokenResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode issued = (await tokenResponse.Content.ReadFromJsonAsync<JsonNode>())!;
        string outstandingCode = await AuthorizeAsync();
        (await human.PostAsJsonAsync("/api/admin/security/credential-cutovers", new { OperationId = operation })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        human.DefaultRequestHeaders.Add("X-OIS-Administrative-Approval", "acknowledge");
        HttpResponseMessage cutover = await human.PostAsJsonAsync("/api/admin/security/credential-cutovers", new { OperationId = operation });
        cutover.StatusCode.ShouldBe(HttpStatusCode.OK);
        CredentialCutoverResult completed = (await cutover.Content.ReadFromJsonAsync<CredentialCutoverResult>())!;
        completed.OperationId.ShouldBe(operation);
        completed.Tokens.ShouldBeGreaterThan(0);
        completed.Sessions.ShouldBeGreaterThan(0);
        (await human.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await machine.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await human.PostAsync("/connect/token", new FormUrlEncodedContent(ExchangeCode(outstandingCode)))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await human.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["client_id"] = grantClient, ["client_secret"] = grantSecret,
            ["refresh_token"] = issued["refresh_token"]!.GetValue<string>()
        }))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        HttpResponseMessage staleCookie = await human.GetAsync(authorizeUrl);
        staleCookie.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        staleCookie.Headers.Location!.ToString().ShouldContain("/Account/Login");
        await fixture.ExecuteDbContextAsync(async db =>
        {
            (await db.UserSessions.AnyAsync(x => x.UserId == new UserId(userId) && x.Status == SessionStatus.Active)).ShouldBeFalse();
        });
        using HttpClient recovered = await helper.SignInHumanAsync(email, password);
        (await recovered.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.OK);
        recovered.DefaultRequestHeaders.Add("X-OIS-Administrative-Approval", "acknowledge");
        HttpResponseMessage retry = await recovered.PostAsJsonAsync("/api/admin/security/credential-cutovers", new { OperationId = operation });
        retry.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await retry.Content.ReadFromJsonAsync<CredentialCutoverResult>()).ShouldBe(completed);
        (await recovered.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
