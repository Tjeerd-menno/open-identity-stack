using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using OpenIdentityStack.Api.Tests.Fixtures;
using OpenIdentityStack.Application.AdministrativeAccess;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Resources;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Authentication;

public sealed class FederatedAdministrativeSignInTests
{
    private const string Upstream = "https://issuer.staff.example.test";
    private const string UpstreamClient = "staff-application";
    private const string TrustedAcr = "urn:staff:hardware-mfa";
    private const string ManagementRedirect = "https://management.example.test/auth/callback";

    [Theory]
    [InlineData(TrustedAcr, true)]
    [InlineData("urn:staff:password", false)]
    public async Task ManagementWebPkceRequiresApprovedUpstreamAssurance(string acr, bool allowed)
    {
        await using var fixture = new AppHostFixture($"federated-mfa-{Guid.NewGuid():N}");
        await fixture.InitializeAsync();
        UpstreamProvider provider = UpstreamProvider.Create("staff", "Staff identity", Upstream, UpstreamClient).Value;
        User user = User.ProvisionFederated("operator@example.test", "Operator", provider.Id, provider.Name, "operator", Upstream).Value;
        Guid roleId = await fixture.CreateTestRoleAsync("user-reader");
        await fixture.ExecuteDbContextAsync(async db =>
        {
            db.UpstreamProviders.Add(provider);
            db.Users.Add(user);
            (await db.Roles.SingleAsync(role => role.Name == "user-reader")).SetPermissions([Permissions.Users.Read]);
            await db.SaveChangesAsync();
        });
        await fixture.AssignRoleToUserAsync(user.Id.Value, roleId);

        using var rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa) { KeyId = "test-issuer" };
        var issuer = new TestIssuer(signingKey, acr);
        using var backchannel = new HttpClient(issuer);
        await using WebApplicationFactory<Program> host = fixture.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdministrativeMfa:Enforce"] = "true",
                [$"AdministrativeMfa:TrustedProviders:{provider.Id.Value}:Issuer"] = Upstream,
                [$"AdministrativeMfa:TrustedProviders:{provider.Id.Value}:Acr"] = TrustedAcr
            }));
            builder.ConfigureTestServices(services => services.PostConfigure<OpenIdConnectOptions>(provider.Name, options =>
            {
                var metadata = new OpenIdConnectConfiguration
                {
                    Issuer = Upstream, AuthorizationEndpoint = Upstream + "/authorize",
                    TokenEndpoint = Upstream + "/token", UserInfoEndpoint = Upstream + "/userinfo"
                };
                metadata.SigningKeys.Add(new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = signingKey.KeyId });
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
                options.Backchannel = backchannel;
            }));
        });
        using IServiceScope scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ResourceAccessBootstrapper>().InitializeAsync();
        Result prepared = await scope.ServiceProvider.GetRequiredService<ManagementWebPreparation>()
            .PrepareAsync([ManagementRedirect], [], bootstrapApproval: true);
        prepared.IsSuccess.ShouldBeTrue();
        using HttpClient browser = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://issuer.example.com"), AllowAutoRedirect = false
        });
        string verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        string authorize = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["response_type"] = "code", ["client_id"] = ManagementWebPreparation.ClientId,
            ["redirect_uri"] = ManagementRedirect, ["scope"] = "openid profile email ois.admin",
            ["state"] = "management-state", ["code_challenge_method"] = "S256",
            ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
        });
        using HttpResponseMessage challenge = await browser.GetAsync(QueryHelpers.AddQueryString(
            "/Account/ExternalLogin", new Dictionary<string, string?> { ["provider"] = provider.Name, ["returnUrl"] = authorize }));
        challenge.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        Uri upstreamLogin = challenge.Headers.Location!;
        upstreamLogin.GetLeftPart(UriPartial.Path).ShouldBe(Upstream + "/authorize");
        Dictionary<string, StringValues> upstreamRequest = QueryHelpers.ParseQuery(upstreamLogin.Query);
        issuer.Nonce = upstreamRequest["nonce"].Single()!;
        using HttpResponseMessage upstreamCallback = await browser.PostAsync(upstreamRequest["redirect_uri"].Single(),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = "upstream-code", ["state"] = upstreamRequest["state"].Single()!
            }));
        upstreamCallback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        using HttpResponseMessage localLogin = await browser.GetAsync(upstreamCallback.Headers.Location);
        localLogin.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        localLogin.Headers.Location!.OriginalString.ShouldBe(authorize);
        using HttpResponseMessage authorization = await browser.GetAsync(localLogin.Headers.Location);
        authorization.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        Uri managementCallback = authorization.Headers.Location!;
        managementCallback.GetLeftPart(UriPartial.Path).ShouldBe(ManagementRedirect);
        Dictionary<string, StringValues> parameters = QueryHelpers.ParseQuery(managementCallback.Query);
        parameters["state"].Single().ShouldBe("management-state");
        if (!allowed)
        {
            parameters["error"].Single().ShouldBe("access_denied");
            parameters.ContainsKey("code").ShouldBeFalse();
            return;
        }
        using HttpResponseMessage exchange = await browser.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["client_id"] = ManagementWebPreparation.ClientId,
            ["redirect_uri"] = ManagementRedirect, ["code"] = parameters["code"].Single()!, ["code_verifier"] = verifier
        }));
        exchange.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode tokens = (await exchange.Content.ReadFromJsonAsync<JsonNode>())!;
        browser.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens["access_token"]!.GetValue<string>());
        JsonNode currentUser = (await browser.GetFromJsonAsync<JsonNode>("/api/me"))!;
        currentUser["subject"]!.GetValue<string>().ShouldBe(user.Id.Value.ToString());
        currentUser["permissions"]!.AsArray().Select(value => value!.GetValue<string>()).ShouldContain(Permissions.Users.Read);
    }

    private sealed class TestIssuer(SecurityKey key, string acr) : HttpMessageHandler
    {
        public string? Nonce { private get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/userinfo")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { sub = "operator" }) };
            }
            request.RequestUri?.AbsolutePath.ShouldBe("/token");
            Dictionary<string, StringValues> form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
            form["code"].Single().ShouldBe("upstream-code");
            string idToken = new JwtSecurityTokenHandler().CreateEncodedJwt(new SecurityTokenDescriptor
            {
                Issuer = Upstream, Audience = UpstreamClient, Expires = DateTime.UtcNow.AddMinutes(5),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
                Claims = new Dictionary<string, object>
                {
                    ["sub"] = "operator", ["nonce"] = Nonce!, ["acr"] = acr,
                    ["auth_time"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), ["email"] = "operator@example.test"
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { access_token = "fixture-access-token", token_type = "Bearer", id_token = idToken })
            };
        }
    }
}
