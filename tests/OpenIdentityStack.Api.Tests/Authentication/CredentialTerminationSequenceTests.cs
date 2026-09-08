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
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Authentication;

public sealed class CredentialTerminationSequenceTests(AppHostFixture fixture)
{
    private const string password = "Password123!@#";
    private const string clientSecret = "sequence-client-secret";
    private const string redirectUri = "https://localhost/callback";
    private static readonly string[] sessionReadPermissions = ["sessions:read"];

    [Theory]
    [InlineData("revoke")]
    [InlineData("revoke-all")]
    [InlineData("reset")]
    [InlineData("disable")]
    [InlineData("delete-session")]
    [InlineData("form-logout")]
    [InlineData("oidc-logout")]
    public async Task Termination_RejectsRetainedAccessRefreshCodeAndCookie(string operation)
    {
        string email = $"termination-{Guid.NewGuid():N}@example.test";
        Guid userId = await fixture.CreateTestUserAsync(email, "Termination User", password);
        string clientId = $"termination-{Guid.NewGuid():N}";
        await fixture.CreateServiceAccountAsync(clientId, clientSecret,
            allowedScopes: ["openid", "offline_access"],
            allowedGrantTypes: ["authorization_code", "refresh_token"],
            redirectUris: [redirectUri],
            postLogoutRedirectUris: [redirectUri]);

        using HttpClient browser = fixture.CreateClient(allowAutoRedirect: false);
        string loginPage = await browser.GetStringAsync("/Account/Login");
        using HttpResponseMessage login = await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = ExtractAntiForgeryToken(loginPage)
        }));
        login.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        string retainedCookie = string.Join("; ", login.Headers.GetValues("Set-Cookie")
            .Select(value => value.Split(';', 2)[0]));

        (string code, string verifier) = await ObtainCodeAsync(browser, clientId);
        using HttpResponseMessage issued = await ExchangeCodeAsync(browser, clientId, code, verifier);
        issued.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode tokens = (await issued.Content.ReadFromJsonAsync<JsonNode>())!;
        string accessToken = tokens["access_token"]!.GetValue<string>();
        string refreshToken = tokens["refresh_token"]!.GetValue<string>();
        (string unusedCode, string unusedVerifier) = await ObtainCodeAsync(browser, clientId);

        Guid sessionId = Guid.Empty;
        await fixture.ExecuteDbContextAsync(async context =>
        {
            sessionId = (await context.UserSessions.SingleAsync(session => session.UserId == new UserId(userId))).Id.Value;
        });

        using HttpClient bearer = fixture.CreateClient(allowAutoRedirect: false);
        bearer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        // An ordinary user token is valid at UserInfo, but deliberately lacks the
        // dedicated administrative-resource entitlement required by /api/me.
        using HttpResponseMessage beforeUserInfo = await bearer.GetAsync("/connect/userinfo");
        beforeUserInfo.StatusCode.ShouldBe(HttpStatusCode.OK);
        using HttpResponseMessage before = await bearer.GetAsync("/api/me");
        before.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using HttpClient otherBrowser = fixture.CreateClient(allowAutoRedirect: false);
        await LoginAsync(otherBrowser, email);
        (string otherCode, string otherVerifier) = await ObtainCodeAsync(otherBrowser, clientId);
        using HttpResponseMessage otherIssued = await ExchangeCodeAsync(otherBrowser, clientId, otherCode, otherVerifier);
        otherIssued.StatusCode.ShouldBe(HttpStatusCode.OK);
        string otherAccessToken = (await otherIssued.Content.ReadFromJsonAsync<JsonNode>())!["access_token"]!.GetValue<string>();

        using HttpClient admin = await fixture.CreateAuthenticatedClientAsync($"termination-admin-{Guid.NewGuid():N}", clientSecret);
        string? confirmationToken = null;
        if (operation == "oidc-logout")
        {
            using HttpResponseMessage confirmation = await browser.PostAsync("/connect/logout", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["post_logout_redirect_uri"] = redirectUri,
                ["state"] = "protocol-post"
            }));
            confirmation.StatusCode.ShouldBe(HttpStatusCode.OK);
            string confirmationPage = await confirmation.Content.ReadAsStringAsync();
            confirmationPage.ShouldContain("name=\"client_id\" value=\"" + clientId + "\"");
            confirmationPage.ShouldContain("name=\"post_logout_redirect_uri\" value=\"" + redirectUri + "\"");
            confirmationPage.ShouldContain("name=\"state\" value=\"protocol-post\"");
            confirmationPage.ShouldContain("name=\"confirm_logout\" value=\"true\"");
            confirmationToken = ExtractAntiForgeryToken(confirmationPage);
            using HttpResponseMessage stillActive = await bearer.GetAsync("/connect/userinfo");
            stillActive.StatusCode.ShouldBe(HttpStatusCode.OK);
            using HttpResponseMessage csrf = await browser.PostAsync("/connect/logout", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["post_logout_redirect_uri"] = redirectUri,
                ["state"] = "protocol-post",
                ["confirm_logout"] = "true"
            }));
            csrf.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            using HttpResponseMessage rejectedRedirect = await browser.PostAsync("/connect/logout", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["post_logout_redirect_uri"] = "https://localhost/unregistered-logout",
                ["state"] = "protocol-post"
            }));
            rejectedRedirect.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
        if (operation == "delete-session")
        {
            await fixture.ExecuteDbContextAsync(async context =>
            {
                UserSession session = await context.UserSessions.SingleAsync(item => item.Id == new SessionId(sessionId));
                context.UserSessions.Remove(session);
                await context.SaveChangesAsync();
            });
        }
        else
        {
            using HttpResponseMessage terminated = operation switch
            {
                "revoke" => await admin.DeleteAsync($"/api/admin/sessions/{sessionId}"),
                "revoke-all" => await admin.DeleteAsync($"/api/admin/users/{userId}/sessions"),
                "reset" => await admin.PostAsJsonAsync($"/api/admin/users/{userId}/reset-password", new { NewPassword = "ChangedPassword123!@#" }),
                "disable" => await admin.PostAsJsonAsync($"/api/admin/users/{userId}/disable", new { Reason = "Credential termination test" }),
                "oidc-logout" => await browser.PostAsync("/connect/logout", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["post_logout_redirect_uri"] = redirectUri,
                    ["state"] = "protocol-post",
                    ["confirm_logout"] = "true",
                    ["__RequestVerificationToken"] = confirmationToken!
                })),
                "form-logout" => await browser.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = ExtractAntiForgeryToken(await browser.GetStringAsync("/Account/Login"))
                })),
                _ => throw new InvalidOperationException("Unknown termination action.")
            };
            (terminated.IsSuccessStatusCode || terminated.StatusCode == HttpStatusCode.Redirect).ShouldBeTrue();
            if (operation == "oidc-logout")
            {
                terminated.StatusCode.ShouldBe(HttpStatusCode.Redirect);
                terminated.Headers.Location!.ToString().ShouldBe(redirectUri + "?state=protocol-post");
            }
        }

        if (operation == "disable")
        {
            using HttpResponseMessage enabled = await admin.PostAsync($"/api/admin/users/{userId}/enable", null);
            enabled.IsSuccessStatusCode.ShouldBeTrue();
        }

        using HttpClient otherBearer = fixture.CreateClient(allowAutoRedirect: false);
        otherBearer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherAccessToken);
        using HttpResponseMessage otherAccess = await otherBearer.GetAsync("/api/me");
        otherAccess.StatusCode.ShouldBe(operation is "revoke-all" or "reset" or "disable"
            ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden);
        using HttpResponseMessage machineAccess = await admin.GetAsync("/api/me");
        machineAccess.StatusCode.ShouldBe(HttpStatusCode.OK);

        using HttpResponseMessage retainedAccess = await bearer.GetAsync("/api/me");
        retainedAccess.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using HttpResponseMessage userInfo = await bearer.GetAsync("/connect/userinfo");
        userInfo.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using HttpResponseMessage introspection = await browser.PostAsync("/connect/introspect", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["token"] = accessToken
        }));
        introspection.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await introspection.Content.ReadFromJsonAsync<JsonNode>())!["active"]!.GetValue<bool>().ShouldBeFalse();

        using HttpResponseMessage refreshed = await browser.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["refresh_token"] = refreshToken
        }));
        refreshed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await refreshed.Content.ReadFromJsonAsync<JsonNode>())!["error"]!.GetValue<string>().ShouldBe("invalid_grant");

        using HttpResponseMessage redeemed = await ExchangeCodeAsync(browser, clientId, unusedCode, unusedVerifier);
        redeemed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await redeemed.Content.ReadFromJsonAsync<JsonNode>())!["error"]!.GetValue<string>().ShouldBe("invalid_grant");

        using HttpClient stolenCookie = fixture.CreateClient(allowAutoRedirect: false);
        stolenCookie.DefaultRequestHeaders.Add("Cookie", retainedCookie);
        using HttpResponseMessage silent = await stolenCookie.GetAsync(await BuildAuthorizeUrlAsync(clientId, "none"));
        silent.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        QueryHelpers.ParseQuery(silent.Headers.Location!.Query)["error"].ToString().ShouldBe("login_required");
    }

    private static async Task LoginAsync(HttpClient browser, string email)
    {
        string page = await browser.GetStringAsync("/Account/Login");
        using HttpResponseMessage response = await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = ExtractAntiForgeryToken(page)
        }));
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task RemovedLastRole_PreventsRetainedAccessAndRefresh()
    {
        string email = $"privilege-{Guid.NewGuid():N}@example.test";
        Guid userId = await fixture.CreateTestUserAsync(email, "Privilege User", password);
        using HttpClient admin = await fixture.CreateAuthenticatedClientAsync($"privilege-admin-{Guid.NewGuid():N}", clientSecret);
        using HttpResponseMessage createdRole = await admin.PostAsJsonAsync("/api/admin/roles", new
        {
            Name = $"session-reader-{Guid.NewGuid():N}",
            DisplayName = "Session reader",
            Permissions = sessionReadPermissions
        });
        createdRole.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid roleId = (await createdRole.Content.ReadFromJsonAsync<JsonNode>())!["id"]!.GetValue<Guid>();
        await fixture.AssignRoleToUserAsync(userId, roleId);
        HumanAdministrativeSession administrativeSession = await HumanAdministrativeSession.SignInAsync(
            fixture, email, password, sessionReadPermissions);
        using HttpClient bearer = administrativeSession.Client;
        using HttpResponseMessage allowed = await bearer.GetAsync("/api/admin/sessions");
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);

        using HttpResponseMessage removed = await admin.DeleteAsync($"/api/admin/users/{userId}/roles/{roleId}");
        removed.IsSuccessStatusCode.ShouldBeTrue();
        using HttpResponseMessage denied = await bearer.GetAsync("/api/admin/sessions");
        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using HttpResponseMessage refreshed = await bearer.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = administrativeSession.ClientId,
            ["client_secret"] = administrativeSession.ClientSecret,
            ["refresh_token"] = administrativeSession.RefreshToken
        }));
        refreshed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await refreshed.Content.ReadFromJsonAsync<JsonNode>())!["error"]!.GetValue<string>().ShouldBe("invalid_grant");
    }

    private static async Task<(string Code, string Verifier)> ObtainCodeAsync(HttpClient browser, string clientId)
    {
        string verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        using HttpResponseMessage authorization = await browser.GetAsync(await BuildAuthorizeUrlAsync(clientId, null, verifier));
        authorization.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        string code = QueryHelpers.ParseQuery(authorization.Headers.Location!.Query)["code"].ToString();
        code.ShouldNotBeNullOrEmpty();
        return (code, verifier);
    }

    private static Task<HttpResponseMessage> ExchangeCodeAsync(HttpClient browser, string clientId, string code, string verifier) =>
        browser.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = verifier
        }));

    private static async Task<string> BuildAuthorizeUrlAsync(string clientId, string? prompt = null, string? verifier = null)
    {
        verifier ??= WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var parameters = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = "openid offline_access",
            ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))),
            ["code_challenge_method"] = "S256"
        };
        if (prompt is not null)
        {
            parameters["prompt"] = prompt;
        }
        using var form = new FormUrlEncodedContent(parameters);
        return "/connect/authorize?" + await form.ReadAsStringAsync();
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        Match match = Regex.Match(html, "<input[^>]+name=\"__RequestVerificationToken\"[^>]+value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
        match.Success.ShouldBeTrue();
        return WebUtility.HtmlDecode(match.Groups["value"].Value);
    }
}
