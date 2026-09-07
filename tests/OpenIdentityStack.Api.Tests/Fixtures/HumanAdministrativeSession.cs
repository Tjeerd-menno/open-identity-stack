using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Domain.Applications;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Fixtures;

internal sealed record HumanAdministrativeSession(
    HttpClient Client,
    string ClientId,
    string ClientSecret,
    string RefreshToken,
    string MonitoringCookie)
{
    public static async Task<HumanAdministrativeSession> SignInAsync(AppHostFixture fixture, string email, string password, IReadOnlyList<string> ceiling)
    {
        string clientId = $"human-admin-{Guid.NewGuid():N}";
        const string secret = "test-client-secret";
        const string redirect = "https://localhost/callback";
        await fixture.CreateServiceAccountAsync(clientId, secret, ["openid", "ois.admin", "offline_access"],
            ["authorization_code", "refresh_token"], [redirect]);
        await fixture.ExecuteDbContextAsync(async db =>
        {
            OpenIdentityStack.Domain.Applications.Application? application = await db.Applications.SingleOrDefaultAsync(client => client.ClientId == clientId);
            if (application is null)
            {
                IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
                clock.UtcNow.Returns(DateTimeOffset.UtcNow);
                application = OpenIdentityStack.Domain.Applications.Application.Create(clientId, "Human administrative test", null,
                    ApplicationProfile.Web, OAuthClientType.Confidential, ["authorization_code", "refresh_token"],
                    ["openid", "ois.admin", "offline_access"], [redirect], [], true, false, clock).Value;
                db.Applications.Add(application);
            }
            if (!await db.Set<ProtectedResource>().AnyAsync(resource => resource.Id == ProtectedResource.AdministrativeResourceId))
            {
                db.Set<ProtectedResource>().Add(ProtectedResource.CreateAdministrative());
            }
            db.Set<ClientResourceGrant>().Add(ClientResourceGrant.Create(application.Id, ProtectedResource.AdministrativeResourceId, ceiling, []).Value);
            await db.SaveChangesAsync();
        });
        string verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        string challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        string query = await new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["response_type"] = "code", ["client_id"] = clientId, ["redirect_uri"] = redirect,
            ["scope"] = "openid ois.admin offline_access", ["code_challenge"] = challenge, ["code_challenge_method"] = "S256",
        }).ReadAsStringAsync();
        HttpClient client = fixture.CreateClient(allowAutoRedirect: false);
        HttpResponseMessage page = await client.GetAsync("/Account/Login");
        Match match = Regex.Match(await page.Content.ReadAsStringAsync(), "<input[^>]+name=\"__RequestVerificationToken\"[^>]+value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
        match.Success.ShouldBeTrue();
        HttpResponseMessage login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = password, ["returnUrl"] = "/connect/authorize?" + query,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups["value"].Value),
        }));
        login.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        string monitoringCookie = login.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("op_session=", StringComparison.Ordinal))
            .Split(';', 2)[0]["op_session=".Length..];
        HttpResponseMessage authorize = await client.GetAsync(login.Headers.Location);
        authorize.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        string code = QueryHelpers.ParseQuery(authorize.Headers.Location!.Query)["code"].Single()!;
        HttpResponseMessage tokenResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = redirect, ["code_verifier"] = verifier,
            ["client_id"] = clientId, ["client_secret"] = secret,
        }));
        tokenResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode tokens = (await tokenResponse.Content.ReadFromJsonAsync<JsonNode>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens["access_token"]!.GetValue<string>());
        return new(client, clientId, secret, tokens["refresh_token"]!.GetValue<string>(), monitoringCookie);
    }
}
