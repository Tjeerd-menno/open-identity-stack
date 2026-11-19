using Microsoft.Extensions.Hosting;
using OpenIdentityStack.Api.Authorization;

namespace OpenIdentityStack.Testing;

internal static class AdministrativeMfaTestPolicy
{
    public static readonly Guid ProviderId = Guid.Parse("82dcdbf2-c84f-47f2-a499-6f3d46e7b7f2");
    public const string Issuer = "https://assurance.example.test";
    public const string Acr = "urn:example:phishing-resistant";

    public static AdministrativeMfaPolicy Create(string environment = "Testing")
    {
        HostApplicationBuilder host = new(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            EnvironmentName = environment
        });
        host.Configuration[$"AdministrativeMfa:TrustedProviders:{ProviderId}:Issuer"] = Issuer;
        host.Configuration[$"AdministrativeMfa:TrustedProviders:{ProviderId}:Acr"] = Acr;
        return new AdministrativeMfaPolicy(host.Environment, host.Configuration, TimeProvider.System);
    }
}
