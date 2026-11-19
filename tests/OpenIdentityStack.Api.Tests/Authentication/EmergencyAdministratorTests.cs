using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using OpenIdentityStack.Api.Tests.Fixtures;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Settings;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Authentication;

public sealed class EmergencyAdministratorTests
{
    [Fact]
    public async Task RecoveryDuringProviderOutageRequiresDesignatedAccountPasswordAndTlsCertificate()
    {
        const string password = "Fixture-Password-Only!73";
        const string emergencyEmail = "emergency@example.test";
        const string operatorEmail = "operator@example.test";
        await using var fixture = new AppHostFixture($"emergency-{Guid.NewGuid():N}");
        await fixture.InitializeAsync();
        Guid emergencyId = await fixture.CreateTestUserAsync(emergencyEmail, "Emergency Administrator", password);
        Guid operatorId = await fixture.CreateTestUserAsync(operatorEmail, "Routine Administrator", password);
        Guid roleId = await fixture.CreateTestRoleAsync("recovery-drill");
        await fixture.AssignRoleToUserAsync(emergencyId, roleId);
        await fixture.AssignRoleToUserAsync(operatorId, roleId);
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        await fixture.ExecuteDbContextAsync(async db =>
        {
            (await db.Roles.SingleAsync(role => role.Name == "recovery-drill")).SetPermissions([Permissions.All]);
            AuthenticationSettings? settings = await db.AuthenticationSettings.SingleOrDefaultAsync();
            if (settings is null)
            {
                settings = AuthenticationSettings.CreateDefault(clock);
                db.AuthenticationSettings.Add(settings);
            }
            UpstreamProvider provider = UpstreamProvider.Create("drill", "Unavailable drill provider", "https://unavailable.invalid",
                "drill-client").Value;
            db.UpstreamProviders.Add(provider);
            settings.SetDefaultProvider(provider.Id, clock).IsSuccess.ShouldBeTrue();
            settings.EnableLocalFallback(clock).IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync();
        });

        using X509Certificate2 serverCertificate = CreateCertificate("server", "1.3.6.1.5.5.7.3.1");
        using X509Certificate2 clientCertificate = CreateCertificate("emergency", "1.3.6.1.5.5.7.3.2");
        await using WebApplicationFactory<Program> host = fixture.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdministrativeMfa:Enforce"] = "true",
                ["AdministrativeMfa:Emergency:UserId"] = emergencyId.ToString(),
                ["AdministrativeMfa:Emergency:CertificateSha256"] = clientCertificate.GetCertHashString(HashAlgorithmName.SHA256)
            })));
        host.UseKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0, listener => listener.UseHttps(serverCertificate));
            options.Listen(IPAddress.Loopback, 0, listener => listener.UseHttps(https =>
            {
                https.ServerCertificate = serverCertificate;
                https.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
                https.ClientCertificateValidation = (cert, _, _) => cert.Equals(clientCertificate);
            }));
        });
        IServerAddressesFeature addresses = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        Uri[] listeners = addresses.Addresses.Where(address => address.StartsWith("https://", StringComparison.Ordinal)).Select(address => new Uri(address)).ToArray();
        listeners.Length.ShouldBe(2);
        using HttpClient noCertificate = CreateBrowser(listeners[1], serverCertificate);
        using HttpClient routine = CreateBrowser(listeners[1], serverCertificate, clientCertificate);
        var cookies = new CookieContainer();
        using HttpClient emergency = CreateBrowser(listeners[1], serverCertificate, clientCertificate, cookies);
        using HttpClient canonicalIssuer = CreateBrowser(listeners[0], serverCertificate, cookies: cookies);
        await AssertLoginDeniedAsync(emergency, emergencyEmail, "Incorrect fixture password");
        await AssertLoginDeniedAsync(noCertificate, emergencyEmail, password);
        await AssertLoginDeniedAsync(routine, operatorEmail, password);
        await fixture.ExecuteDbContextAsync(async db =>
        {
            (await db.Users.Where(user => user.Id == new UserId(emergencyId) || user.Id == new UserId(operatorId))
                .Select(user => user.LastLoginAt).ToListAsync()).ShouldAllBe(lastLogin => lastLogin == null);
            (await db.AuditLogEntries.AnyAsync(entry => entry.Action == "LocalFallbackAuthentication")).ShouldBeFalse();
        });
        Guid disposableSession = await fixture.CreateSessionAsync(operatorId);
        HumanAdministrativeSession session = await HumanAdministrativeSession.SignInAsync(fixture, emergencyEmail,
            password, [Permissions.Sessions.Revoke], canonicalIssuer, emergency);
        using HttpResponseMessage recovered = await session.Client.DeleteAsync($"/api/admin/sessions/{disposableSession}");
        recovered.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using HttpClient passwordOnly = CreateBrowser(listeners[0], serverCertificate);
        using HttpResponseMessage localLogin = await PostLoginAsync(passwordOnly, operatorEmail, password);
        localLogin.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        string authorizationUrl = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = session.ClientId, ["redirect_uri"] = "https://localhost/callback",
            ["response_type"] = "code", ["scope"] = "openid ois.admin",
            ["code_challenge"] = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)), ["code_challenge_method"] = "S256"
        });
        using HttpResponseMessage insufficient = await passwordOnly.GetAsync(authorizationUrl);
        insufficient.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(insufficient.Headers.Location!.Query);
        query["error"].ToString().ShouldBe("access_denied");
        query.ContainsKey("code").ShouldBeFalse();

        await fixture.ExecuteDbContextAsync(async db =>
        {
            (await db.UserSessions.SingleAsync(item => item.Id == new SessionId(disposableSession))).Status.ShouldBe(SessionStatus.Revoked);
            (await db.AuthenticationSettings.SingleAsync()).SetDefaultToLocal(clock).IsSuccess.ShouldBeTrue();
            (await db.Roles.SingleAsync(role => role.Name == "recovery-drill")).SetPermissions([]);
            await db.SaveChangesAsync();
        });
        using HttpClient withdrawn = CreateBrowser(listeners[1], serverCertificate, clientCertificate);
        await AssertLoginDeniedAsync(withdrawn, emergencyEmail, password);
        await fixture.ExecuteDbContextAsync(async db =>
        {
            var events = await db.AuditLogEntries.Where(entry => entry.Action.StartsWith("EmergencyAdministrator."))
                .Select(entry => new { entry.Action, entry.UserId, entry.EntityId, entry.Details }).ToListAsync();
            events.Count.ShouldBe(4);
            events.Count(entry => entry.Action == "EmergencyAdministrator.Denied" && entry.UserId == emergencyId.ToString()).ShouldBe(2);
            events.Count(entry => entry.Action == "EmergencyAdministrator.Denied" && entry.UserId == operatorId.ToString()).ShouldBe(1);
            events.Count(entry => entry.Action == "EmergencyAdministrator.Authenticated" && entry.UserId == emergencyId.ToString()).ShouldBe(1);
            events.ShouldAllBe(entry => entry.EntityId == entry.UserId && entry.Details == null);
        });
    }

    private static async Task AssertLoginDeniedAsync(HttpClient client, string email, string password)
    {
        using HttpResponseMessage response = await PostLoginAsync(client, email, password);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string email, string password)
    {
        string form = await client.GetStringAsync("/Account/Login");
        Match token = Regex.Match(form, "<input[^>]+name=\"__RequestVerificationToken\"[^>]+value=\"(?<value>[^\"]+)\"");
        token.Success.ShouldBeTrue();
        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = password, ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups["value"].Value)
        }));
    }

    private static HttpClient CreateBrowser(Uri issuer, X509Certificate2 server, X509Certificate2? client = null, CookieContainer? cookies = null)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = cookies ?? new CookieContainer(),
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) => server.Equals(certificate)
        };
        if (client is not null) { handler.ClientCertificates.Add(client); }
        var browser = new HttpClient(handler) { BaseAddress = issuer };
        browser.DefaultRequestHeaders.Host = "issuer.example.com";
        return browser;
    }

    private static X509Certificate2 CreateCertificate(string name, string usage)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}.example.test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid(usage) }, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), null);
    }
}
