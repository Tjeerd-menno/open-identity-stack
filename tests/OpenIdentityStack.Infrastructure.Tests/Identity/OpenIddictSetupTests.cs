using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Quartz;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using OpenIdentityStack.Infrastructure.Identity;
using Quartz;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenIdentityStack.Infrastructure.Tests.Identity;

public sealed class OpenIddictSetupTests
{
    [Fact]
    public void AddOpenIddictConfiguration_RegistersCertificationEndpointSurface()
    {
        using ServiceProvider provider = BuildProvider(new Dictionary<string, string?>
        {
            ["OpenIddict:Issuer"] = "https://id.example.com/"
        });

        OpenIddictServerOptions options = provider.GetRequiredService<IOptions<OpenIddictServerOptions>>().Value;

        options.AuthorizationEndpointUris.ShouldContain(new Uri("https://id.example.com/connect/authorize"));
        options.TokenEndpointUris.ShouldContain(new Uri("https://id.example.com/connect/token"));
        options.UserInfoEndpointUris.ShouldContain(new Uri("https://id.example.com/connect/userinfo"));
        options.EndSessionEndpointUris.ShouldContain(new Uri("https://id.example.com/connect/logout"));
        options.ConfigurationEndpointUris.ShouldContain(new Uri("https://id.example.com/.well-known/openid-configuration"));
        options.JsonWebKeySetEndpointUris.ShouldContain(new Uri("https://id.example.com/.well-known/jwks"));
    }

    [Fact]
    public void AddOpenIddictConfiguration_RegistersProfileClaimsAndScopes()
    {
        using ServiceProvider provider = BuildProvider();

        OpenIddictServerOptions options = provider.GetRequiredService<IOptions<OpenIddictServerOptions>>().Value;

        options.Scopes.ShouldContain(Scopes.Profile);
        options.Scopes.ShouldContain(Scopes.Email);
        options.Scopes.ShouldContain(Scopes.Roles);
        options.Scopes.ShouldContain("api");
        options.Claims.ShouldContain(Claims.PreferredUsername);
        options.Claims.ShouldContain(Claims.GivenName);
        options.Claims.ShouldContain(Claims.FamilyName);
        options.Claims.ShouldContain(Claims.Locale);
        options.Claims.ShouldContain(Claims.UpdatedAt);
        options.Claims.ShouldContain(Claims.EmailVerified);
    }

    [Fact]
    public void AddOpenIddictConfiguration_EnablesAspNetCorePassthroughForHandledEndpoints()
    {
        using ServiceProvider provider = BuildProvider();

        OpenIddictServerAspNetCoreOptions options = provider.GetRequiredService<IOptions<OpenIddictServerAspNetCoreOptions>>().Value;

        options.EnableAuthorizationEndpointPassthrough.ShouldBeTrue();
        options.EnableTokenEndpointPassthrough.ShouldBeTrue();
        options.EnableUserInfoEndpointPassthrough.ShouldBeTrue();
        options.EnableEndSessionEndpointPassthrough.ShouldBeTrue();
        options.DisableTransportSecurityRequirement.ShouldBeTrue();
    }

    [Fact]
    public void AddOpenIddictConfiguration_RegistersQuartzCleanupServices()
    {
        ServiceCollection services = BuildServices();
        using ServiceProvider provider = services.BuildServiceProvider();

        ISchedulerFactory schedulerFactory = provider.GetRequiredService<ISchedulerFactory>();
        schedulerFactory.ShouldNotBeNull();

        provider.GetServices<IHostedService>()
            .Any(service => service is QuartzHostedService)
            .ShouldBeTrue();
    }

    [Fact]
    public void AddOpenIddictConfiguration_AppliesQuartzAdvancedSettings()
    {
        using ServiceProvider provider = BuildProvider(new Dictionary<string, string?>
        {
            ["OpenIddict:Quartz:DisableAuthorizationPruning"] = "true",
            ["OpenIddict:Quartz:DisableTokenPruning"] = "true",
            ["OpenIddict:Quartz:MinimumAuthorizationLifespan"] = "7.00:00:00",
            ["OpenIddict:Quartz:MinimumTokenLifespan"] = "00:30:00",
            ["OpenIddict:Quartz:MaximumRefireCount"] = "4"
        });

        OpenIddictQuartzOptions options = provider.GetRequiredService<IOptions<OpenIddictQuartzOptions>>().Value;

        options.DisableAuthorizationPruning.ShouldBeTrue();
        options.DisableTokenPruning.ShouldBeTrue();
        options.MinimumAuthorizationLifespan.ShouldBe(TimeSpan.FromDays(7));
        options.MinimumTokenLifespan.ShouldBe(TimeSpan.FromMinutes(30));
        options.MaximumRefireCount.ShouldBe(4);
    }

    private static ServiceProvider BuildProvider(IReadOnlyDictionary<string, string?>? values = null)
    {
        return BuildServices(values).BuildServiceProvider();
    }

    private static ServiceCollection BuildServices(IReadOnlyDictionary<string, string?>? values = null)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddOpenIddictConfiguration(configuration, "Testing");
        return services;
    }
}
