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
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Infrastructure.Persistence;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Admin;

public sealed class CredentialCutoverTests
{
    private static readonly bool[] passwordCandidates = [false, true];
    [Fact]
    public async Task PersistedSessionRevocationClearsMonitoringCookieOnCheckSessionRequest()
    {
        await using var fixture = new AppHostFixture($"monitoring-{Guid.NewGuid():N}");
        await fixture.InitializeAsync();
        string email = $"monitoring-{Guid.NewGuid():N}@example.test";
        const string password = "Password123!@#";
        Guid userId = await fixture.CreateTestUserAsync(email, "Session monitor", password);
        HumanAdministrativeSession authenticated = await HumanAdministrativeSession.SignInAsync(fixture, email, password, []);
        using HttpClient browser = authenticated.Client;
        (await browser.GetAsync("/connect/check_session")).StatusCode.ShouldBe(HttpStatusCode.OK);
        using HttpClient thirdPartyIframe = fixture.CreateClient(allowAutoRedirect: false);
        thirdPartyIframe.DefaultRequestHeaders.Add("Cookie", $"op_session={authenticated.MonitoringCookie}");
        HttpResponseMessage initiallyCurrent = await thirdPartyIframe.GetAsync("/connect/check_session");
        initiallyCurrent.StatusCode.ShouldBe(HttpStatusCode.OK);
        initiallyCurrent.Headers.TryGetValues("Set-Cookie", out _).ShouldBeFalse();
        await fixture.ExecuteDbContextAsync(async db =>
        {
            IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
            clock.UtcNow.Returns(DateTimeOffset.UtcNow);
            foreach (UserSession session in await db.UserSessions.Where(value => value.UserId == new UserId(userId)).ToListAsync())
            {
                session.Revoke(clock);
            }
            await db.SaveChangesAsync();
        });

        AssertMonitoringCookieCleared(await thirdPartyIframe.GetAsync("/connect/check_session"));
    }

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
        using HttpClient machine = await fixture.CreateAuthenticatedClientAsync(userId.ToString(), "machine-secret");
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
        await PrepareManagementWebAsync(fixture);
        HttpResponseMessage emergency = await human.PostAsJsonAsync("/api/admin/security/emergency-access-evidence", new { });
        emergency.StatusCode.ShouldBe(HttpStatusCode.OK, await emergency.Content.ReadAsStringAsync());
        var resourceId = Guid.NewGuid();
        await fixture.ExecuteDbContextAsync(async db =>
        {
            ProtectedResource resource = ProtectedResource.Create("urn:rehearsal-business", "rehearsal-business", "Rehearsal business", ["business"]).Value;
            resourceId = resource.Id;
            db.Add(resource);
            await db.SaveChangesAsync();
        });
        (await human.GetFromJsonAsync<CredentialCutoverPreflight>("/api/admin/security/cutover-readiness"))!.Ready.ShouldBeFalse();
        (await human.PutAsJsonAsync($"/api/admin/security/business-resources/{resourceId}/token-window-review", new { Mechanism = "AssumeSafe", ResidualSeconds = 0, EvidenceReference = "" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await human.PutAsJsonAsync($"/api/admin/security/business-resources/{resourceId}/token-window-review", new { Mechanism = "OnlineIntrospection", ResidualSeconds = 30, EvidenceReference = "isolated-fixture:simulated-consumer-control" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        CredentialCutoverPreflight preflight = (await human.GetFromJsonAsync<CredentialCutoverPreflight>("/api/admin/security/cutover-readiness"))!;
        preflight.Ready.ShouldBeTrue(string.Join("; ", preflight.Blockers.Select(x => x.Message)));
        HttpResponseMessage cutover = await human.PostAsJsonAsync("/api/admin/security/credential-cutovers", new { OperationId = operation });
        cutover.StatusCode.ShouldBe(HttpStatusCode.OK);
        CredentialCutoverResult completed = (await cutover.Content.ReadFromJsonAsync<CredentialCutoverResult>())!;
        completed.OperationId.ShouldBe(operation);
        completed.Tokens.ShouldBeGreaterThan(0);
        completed.Sessions.ShouldBeGreaterThan(0);
        AssertMonitoringCookieCleared(await human.GetAsync("/connect/check_session"));
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

    private static void AssertMonitoringCookieCleared(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("op_session=", StringComparison.Ordinal));
        cookie.ShouldContain("op_session=;");
        cookie.ShouldContain("expires=Thu, 01 Jan 1970");
        cookie.ShouldContain("path=/");
        cookie.ShouldContain("secure");
        cookie.ShouldContain("samesite=none");
    }
    [Fact]
    public async Task LegacyRehearsalPreservesQuarantineAndDisablementDespiteTestedEmergencyLogin()
    {
        await using var fixture = new AppHostFixture($"cutover-legacy-{Guid.NewGuid():N}");
        await fixture.InitializeAsync();
        const string password = "Password123!@#";
        string email = $"operator-{Guid.NewGuid():N}@example.com";
        Guid operatorId = await fixture.CreateTestUserAsync(email, "Emergency", password);
        Guid disabledId = await fixture.CreateTestUserAsync($"disabled-{Guid.NewGuid():N}@example.com", "Disabled", password);
        await fixture.DisableUserAsync(disabledId);
        await fixture.ExecuteDbContextAsync(async db =>
        {
            IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
            clock.UtcNow.Returns(DateTimeOffset.UtcNow);
            Role unrestricted = Role.Create("explicit-unrestricted", "Explicit", null).Value;
            unrestricted.SetPermissions(["*"]);
            db.Add(unrestricted);
            db.Add(RoleAssignment.Create(new UserId(operatorId), unrestricted.Id, DateTimeOffset.UtcNow).Value);
            db.Add(Role.Create("admin", "Label only", null).Value);
            UpstreamProvider provider = UpstreamProvider.Create("legacy", "Legacy", "https://issuer.example", "upstream-client").Value;
            db.Add(provider);
            foreach (bool configuredPassword in passwordCandidates)
            {
                User legacy = configuredPassword ? User.CreateLocal("candidate@example.com", "Candidate", "unproven-hash", clock).Value
                    : User.CreateFederated("federation-only@example.com", "Federation only", clock).Value;
                legacy.LinkUpstreamIdentity(provider.Id, provider.Name, Guid.NewGuid().ToString(), legacy.Email, "https://issuer.example");
                db.Add(legacy);
            }
            await db.SaveChangesAsync();
        });
        using HttpClient human = await new UnrestrictedGrantTests(fixture).SignInHumanAsync(email, password);
        human.DefaultRequestHeaders.Add("X-OIS-Administrative-Approval", "acknowledge");
        await PrepareManagementWebAsync(fixture);
        (await human.PostAsJsonAsync("/api/admin/security/emergency-access-evidence", new { })).StatusCode.ShouldBe(HttpStatusCode.OK);
        CredentialCutoverPreflight first = (await human.GetFromJsonAsync<CredentialCutoverPreflight>("/api/admin/security/cutover-readiness"))!;
        CredentialCutoverPreflight second = (await human.GetFromJsonAsync<CredentialCutoverPreflight>("/api/admin/security/cutover-readiness"))!;
        first.Identities.QuarantinedLinks.ShouldBe(2);
        first.Identities.PasswordCandidates.ShouldBe(1);
        first.Identities.FederationOnlyUsers.ShouldBe(1);
        first.Identities.DisabledUsers.ShouldBe(1);
        first.EmergencyAccess!.CurrentlyUsable.ShouldBeTrue();
        second.Identities.ShouldBe(first.Identities);
        first.Ready.ShouldBeFalse();
        var operation = Guid.NewGuid();
        (await human.PostAsJsonAsync("/api/admin/security/credential-cutovers", new { OperationId = operation })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await human.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await fixture.ExecuteDbContextAsync(async db =>
        {
            (await db.Set<CredentialBoundaryState>().SingleAsync()).Epoch.ShouldBe(first.Epoch);
            (await db.Set<CredentialCutoverRecord>().AnyAsync(x => x.Id == operation)).ShouldBeFalse();
            (await db.Users.SingleAsync(x => x.Id == new UserId(disabledId))).Status.ShouldBe(UserStatus.Disabled);
            (await db.Users.ToListAsync()).Sum(x => x.UpstreamIdentities.Count(link => link.IsQuarantined)).ShouldBe(2);
            (await db.Roles.SingleAsync(x => x.Name == "admin")).Permissions.ShouldBeEmpty();
            (await db.Roles.SingleAsync(x => x.Name == "explicit-unrestricted")).Permissions.ShouldContain("*");
            (await db.AuditLogEntries.CountAsync(x => x.Action == "CredentialCutover.PreflightEvaluated")).ShouldBe(2);
            (await db.AuditLogEntries.SingleAsync(x => x.Action == "CredentialCutover.PreflightBlocked")).AfterState!.ShouldContain("Identity.Quarantined");
        });
    }

    private static Task PrepareManagementWebAsync(AppHostFixture fixture) => fixture.ExecuteDbContextAsync(async db =>
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        int[] ports = [5175, 5173, 5174, 3000];
        OpenIdentityStack.Domain.Applications.Application management = OpenIdentityStack.Domain.Applications.Application.Create("management-web-client", "Management", null,
            ApplicationProfile.SinglePage, OAuthClientType.Public, ["authorization_code", "refresh_token"], ["openid", "profile", "email", "ois.admin"],
            ports.SelectMany(port => new[] { $"http://localhost:{port}/auth/callback", $"http://localhost:{port}/auth/silent-callback" }).ToArray(),
            ports.Select(port => $"http://localhost:{port}/").ToArray(), true, false, clock).Value;
        db.Add(management);
        db.Add(ClientResourceGrant.Create(management.Id, ProtectedResource.AdministrativeResourceId, ["*"], []).Value);
        await db.SaveChangesAsync();
    });}
