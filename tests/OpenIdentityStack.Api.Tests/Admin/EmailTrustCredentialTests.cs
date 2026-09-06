using OpenIddict.EntityFrameworkCore.Models;
using SharedKernel;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Api.Tests.Fixtures;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Domain.Roles;

namespace OpenIdentityStack.Api.Tests.Admin;

public sealed class EmailTrustCredentialTests(AppHostFixture fixture)
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task WithdrawalEnforcesIssuedCredentialsAndNewLoginUsesCurrentEvidence(bool independentEvidence, bool lateIssuedState)
    {
        string email = $"withdraw-{Guid.NewGuid():N}@example.test";
        const string password = "Password123!@#";
        string clientId = $"withdraw-client-{Guid.NewGuid():N}";
        const string clientSecret = "test-client-secret";
        Guid userId = await fixture.CreateTestUserAsync(email, "Withdrawal subject", password);
        await fixture.CreateServiceAccountAsync(clientId, clientSecret,
            allowedScopes: ["openid", "email", "ois.admin", "offline_access"],
            allowedGrantTypes: ["authorization_code", "refresh_token"], redirectUris: ["https://localhost/callback"]);
        using HttpClient admin = await fixture.CreateAuthenticatedClientAsync($"withdraw-admin-{Guid.NewGuid():N}", "test-admin-secret");
        UpstreamProvider provider = UpstreamProvider.Create($"withdraw-{Guid.NewGuid():N}", "Provider", "https://issuer.example", "client").Value;
        provider.SetEmailVerificationTrust(true);
        await fixture.ExecuteDbContextAsync(async db =>
        {
            IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
            clock.UtcNow.Returns(DateTimeOffset.UtcNow);
            OpenIdentityStack.Domain.Applications.Application application = await db.Applications.SingleAsync(client => client.ClientId == clientId);
            db.ClientResourceGrants.Add(ClientResourceGrant.Create(application.Id, ProtectedResource.AdministrativeResourceId,
                ["users:read"], []).Value);
            Role role = Role.Create($"email-evidence-reader-{Guid.NewGuid():N}", null).Value;
            role.SetPermissions(["users:read"]);
            db.Roles.Add(role);
            db.RoleAssignments.Add(RoleAssignment.Create(new UserId(userId), role.Id, clock.UtcNow).Value);
            User user = await db.Users.SingleAsync(u => u.Id == new UserId(userId));
            if (!independentEvidence)
            {
                // Seed an active password user whose only evidence comes from the upstream provider.
                db.RemoveRange(user.EmailVerificationEvidence);
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
                user = await db.Users.SingleAsync(u => u.Id == new UserId(userId));
            }
            user.RecordProviderEmailVerification(provider, "https://issuer.example", email, true, DateTimeOffset.UtcNow);
            db.UpstreamProviders.Add(provider);
            await db.SaveChangesAsync();
        });

        using HttpClient retainedBrowser = fixture.CreateClient(allowAutoRedirect: false);
        (JsonNode issued, string authorizationUrl) = await LoginAndIssueAsync(retainedBrowser, email, password, clientId, clientSecret);
        using HttpClient subject = fixture.CreateClient();
        subject.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issued["access_token"]!.GetValue<string>());
        (await subject.GetFromJsonAsync<JsonNode>("/connect/userinfo"))!["email_verified"]!.GetValue<bool>().ShouldBeTrue();
        (await IntrospectAsync(subject, clientId, clientSecret, issued["access_token"]!.GetValue<string>())).ShouldBeTrue();
        // Warm the local validation cache before withdrawal as well as the server token path.
        (await subject.GetAsync("/api/admin/users")).StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);

        (await admin.PutAsJsonAsync($"/api/admin/providers/{provider.Id.Value}/email-verification-trust", new { Trusted = false }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        if (lateIssuedState)
        {
            // Model a still-valid stored token inserted after the bulk revocation query:
            // the signed token keeps the projection's old revision and must still fail both OP paths.
            await fixture.ExecuteDbContextAsync(async db =>
            {
                string subjectId = userId.ToString();
                await db.Set<OpenIddictEntityFrameworkCoreToken>().Where(t => t.Subject == subjectId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.Status, "valid"));
                await db.Set<OpenIddictEntityFrameworkCoreAuthorization>().Where(a => a.Subject == subjectId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.Status, "valid"));
            });
        }
        (await IntrospectAsync(subject, clientId, clientSecret, issued["access_token"]!.GetValue<string>())).ShouldBe(independentEvidence);
        HttpResponseMessage userInfo = await subject.GetAsync("/connect/userinfo");
        userInfo.StatusCode.ShouldBe(independentEvidence ? HttpStatusCode.OK : HttpStatusCode.Unauthorized);
        if (!independentEvidence)
        {
            (await subject.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
        HttpResponseMessage refresh = await subject.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["refresh_token"] = issued["refresh_token"]!.GetValue<string>(),
            ["client_id"] = clientId, ["client_secret"] = clientSecret
        }));
        refresh.StatusCode.ShouldBe(independentEvidence ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
        if (!independentEvidence)
        {
            (await refresh.Content.ReadFromJsonAsync<JsonNode>())!["error"]!.GetValue<string>().ShouldBe("invalid_grant");
        }

        HttpResponseMessage retainedCookie = await retainedBrowser.GetAsync(authorizationUrl);
        retainedCookie.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        Dictionary<string, Microsoft.Extensions.Primitives.StringValues> retainedQuery = QueryHelpers.ParseQuery(retainedCookie.Headers.Location!.Query);
        if (independentEvidence) { retainedQuery.ShouldContainKey("code"); }
        else { retainedQuery["error"].Single().ShouldBe("login_required"); }

        using HttpClient freshBrowser = fixture.CreateClient(allowAutoRedirect: false);
        (JsonNode fresh, _) = await LoginAndIssueAsync(freshBrowser, email, password, clientId, clientSecret);
        subject.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fresh["access_token"]!.GetValue<string>());
        (await subject.GetFromJsonAsync<JsonNode>("/connect/userinfo"))!["email_verified"]!.GetValue<bool>().ShouldBe(independentEvidence);
    }

    private static async Task<bool> IntrospectAsync(HttpClient client, string clientId, string clientSecret, string token)
    {
        HttpResponseMessage response = await client.PostAsync("/connect/introspect", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId, ["client_secret"] = clientSecret, ["token"] = token
        }));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonNode>())!["active"]!.GetValue<bool>();
    }
    private static async Task<(JsonNode Tokens, string AuthorizationUrl)> LoginAndIssueAsync(HttpClient browser, string email, string password, string clientId, string clientSecret)
    {
        string verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        string query = await new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["response_type"] = "code", ["client_id"] = clientId, ["redirect_uri"] = "https://localhost/callback",
            ["scope"] = "openid email ois.admin offline_access", ["state"] = Guid.NewGuid().ToString(), ["nonce"] = Guid.NewGuid().ToString(),
            ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))), ["code_challenge_method"] = "S256"
        }).ReadAsStringAsync();
        HttpResponseMessage authorize = await browser.GetAsync("/connect/authorize?" + query);
        authorize.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        Uri login = new(browser.BaseAddress!, authorize.Headers.Location!);
        string html = await browser.GetStringAsync(login);
        Match match = Regex.Match(html, "<input[^>]+name=\"__RequestVerificationToken\"[^>]+value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
        match.Success.ShouldBeTrue();
        HttpResponseMessage signedIn = await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = password, ["RememberMe"] = "false",
            ["returnUrl"] = QueryHelpers.ParseQuery(login.Query)["returnUrl"].Single()!,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups["value"].Value)
        }));
        signedIn.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        HttpResponseMessage callback = await browser.GetAsync(signedIn.Headers.Location);
        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        string code = QueryHelpers.ParseQuery(callback.Headers.Location!.Query)["code"].Single()!;
        HttpResponseMessage response = await browser.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = "https://localhost/callback",
            ["code_verifier"] = verifier, ["client_id"] = clientId, ["client_secret"] = clientSecret
        }));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return ((await response.Content.ReadFromJsonAsync<JsonNode>())!, "/connect/authorize?" + query + "&prompt=none");
    }
}
