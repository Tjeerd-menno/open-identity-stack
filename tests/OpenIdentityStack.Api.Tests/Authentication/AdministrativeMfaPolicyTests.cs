using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Api.Tests.Fixtures;

namespace OpenIdentityStack.Api.Tests.Authentication;

public sealed class AdministrativeMfaPolicyTests(AppHostFixture fixture)
{
    private static readonly Guid ProviderId = Guid.Parse("ec344c12-b479-4017-b98c-02d233512df1");
    private const string Issuer = "https://idp.example/tenant";
    private const string Acr = "urn:example:staff:hardware-mfa";

    [Fact]
    public void InvalidEmergencyPinStopsStartupBeforeTheServerAcceptsTraffic()
    {
        using WebApplicationFactory<Program> host = fixture.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["AdministrativeMfa:Emergency:CertificateSha256"] = "incomplete" })));
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => host.CreateClient());
        Assert.Contains("AdministrativeMfa emergency access", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Production", false, true)]
    [InlineData("Staging", false, true)]
    [InlineData("Development", false, false)]
    [InlineData("Testing", true, true)]
    public void EnforcementUsesFailClosedEnvironmentDefaults(string environment, bool optIn, bool required)
    {
        AdministrativeMfaPolicy policy = Create(environment, new() { ["AdministrativeMfa:Enforce"] = optIn.ToString() });
        Assert.Equal(required, policy.IsRequired);
        Assert.Equal(!required, policy.AllowsHuman(Principal(), Guid.NewGuid()));
    }

    [Fact]
    public void FederationRequiresExactTrustedContextAndUnambiguousLocalEvidence()
    {
        var settings = new Dictionary<string, string?>
        {
            [$"AdministrativeMfa:TrustedProviders:{ProviderId}:Issuer"] = Issuer,
            [$"AdministrativeMfa:TrustedProviders:{ProviderId}:Acr"] = Acr
        };
        AdministrativeMfaPolicy policy = Create("Production", settings);
        string evidence = Assert.IsType<string>(policy.FederatedEvidence(ProviderId, Issuer, Acr));
        Assert.Null(policy.FederatedEvidence(Guid.NewGuid(), Issuer, Acr));
        Assert.Null(policy.FederatedEvidence(ProviderId, Issuer + "/", Acr));
        Assert.Null(policy.FederatedEvidence(ProviderId, Issuer, "pwd"));
        Assert.True(policy.AllowsHuman(Principal(evidence), Guid.NewGuid()));
        Assert.False(policy.AllowsHuman(Principal(evidence, evidence), Guid.NewGuid()));
        Assert.False(Create("Production", []).AllowsHuman(Principal(evidence), Guid.NewGuid()));
    }

    [Fact]
    public void EmergencyCertificateRequiresDesignatedAccountPinAndCurrentValidity()
    {
        var account = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=rehearsal", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(now.AddMinutes(-1), now.AddHours(2));
        var settings = new Dictionary<string, string?>
        {
            ["AdministrativeMfa:Emergency:UserId"] = account.ToString(),
            ["AdministrativeMfa:Emergency:CertificateSha256"] = certificate.GetCertHashString(HashAlgorithmName.SHA256)
        };
        AdministrativeMfaPolicy policy = Create("Production", settings);
        string evidence = Assert.IsType<string>(policy.EmergencyEvidence(account, certificate));
        Assert.True(policy.AllowsHuman(Principal(evidence), account));
        Assert.False(policy.AllowsHuman(Principal(evidence), Guid.NewGuid()));
        Assert.Null(policy.EmergencyEvidence(Guid.NewGuid(), certificate));
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(now.AddDays(1));
        AdministrativeMfaPolicy expired = Create("Production", settings, clock);
        Assert.Null(expired.EmergencyEvidence(account, certificate));
        Assert.False(expired.AllowsHuman(Principal(evidence), account));
        settings["AdministrativeMfa:Emergency:CertificateSha256"] = new string('0', 64);
        AdministrativeMfaPolicy rotated = Create("Production", settings);
        Assert.False(rotated.AllowsHuman(Principal(evidence), account));
        Assert.Null(rotated.EmergencyEvidence(account, certificate));
    }

    private static AdministrativeMfaPolicy Create(string name, Dictionary<string, string?> settings, TimeProvider? clock = null)
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        return new(environment, new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), clock ?? TimeProvider.System);
    }

    private static ClaimsPrincipal Principal(params string[] evidence) =>
        new(new ClaimsIdentity(evidence.Select(value => new Claim(AdministrativeMfaPolicy.ClaimType, value)), "Cookies"));
}
