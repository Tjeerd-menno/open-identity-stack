using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenIdentityStack.Api.Authentication;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;

using SharedKernel;
namespace OpenIdentityStack.Api.Tests.Authentication;

/// <summary>
/// Unit tests for DynamicAuthenticationSchemeService.
/// </summary>
public sealed class DynamicAuthenticationSchemeServiceTests
{
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("false", "person@example.com", null)]
    [InlineData("true", null, null)]
    [InlineData("true", "person@example.com", "person@example.com")]
    public async Task RegisterSchemeAsync_CapturesOnlySerializableValidatedIssuerAndEmailEvidence(
        string? verified,
        string? email,
        string? expectedEvidence)
    {
        IAuthenticationSchemeProvider schemes = Substitute.For<IAuthenticationSchemeProvider>();
        var cache = new OptionsCache<OpenIdConnectOptions>();
        IServiceProvider services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IEnumerable<IPostConfigureOptions<OpenIdConnectOptions>>)).Returns(Array.Empty<IPostConfigureOptions<OpenIdConnectOptions>>());
        var service = new DynamicAuthenticationSchemeService(schemes, cache, services, Substitute.For<ILogger<DynamicAuthenticationSchemeService>>());
        UpstreamProvider provider = UpstreamProvider.Create("tenant", "Tenant", "https://discovery.example/common", "client").Value;
        await service.RegisterSchemeAsync(provider);
        OpenIdConnectOptions options = cache.GetOrAdd("tenant", () => throw new InvalidOperationException());
        var properties = new AuthenticationProperties();
        var claims = new List<System.Security.Claims.Claim>();
        if (email is not null)
        {
            claims.Add(new System.Security.Claims.Claim("email", email));
        }
        if (verified is not null)
        {
            claims.Add(new System.Security.Claims.Claim("email_verified", verified));
        }
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims, "tenant"));
        var context = new TokenValidatedContext(new Microsoft.AspNetCore.Http.DefaultHttpContext(), new AuthenticationScheme("tenant", "Tenant", typeof(OpenIdConnectHandler)), options, principal, properties)
        {
            SecurityToken = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(issuer: "https://issuer.example/tenant/",
                claims: claims)
        };
        await options.Events.TokenValidated(context);
        properties.GetString("ois.validated_issuer").ShouldBe("https://issuer.example/tenant/");
        properties.GetString("ois.provider_id").ShouldBe(provider.Id.Value.ToString());
        properties.GetString("ois.authentication_authority").ShouldBe("https://discovery.example/common");
        properties.GetString(ExternalIdentityProperties.VerifiedEmail).ShouldBe(expectedEvidence);
        properties.Items.Values.ShouldAllBe(value => value != null);

        var ticket = new AuthenticationTicket(principal, properties, "ExternalCookie");
        byte[] serialized = TicketSerializer.Default.Serialize(ticket);
        TicketSerializer.Default.Deserialize(serialized).ShouldNotBeNull();
    }
    [Fact]
    public async Task RegisterSchemeAsync_AddsSchemeToProvider()
    {
        // Arrange
        IAuthenticationSchemeProvider schemeProvider = Substitute.For<IAuthenticationSchemeProvider>();
        IOptionsMonitorCache<OpenIdConnectOptions> optionsCache = Substitute.For<IOptionsMonitorCache<OpenIdConnectOptions>>();
        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        ILogger<DynamicAuthenticationSchemeService> logger = Substitute.For<ILogger<DynamicAuthenticationSchemeService>>();

        schemeProvider.GetSchemeAsync(Arg.Any<string>()).Returns((AuthenticationScheme?)null);
        serviceProvider.GetService(typeof(IEnumerable<IPostConfigureOptions<OpenIdConnectOptions>>))
            .Returns(Array.Empty<IPostConfigureOptions<OpenIdConnectOptions>>());

        var service = new DynamicAuthenticationSchemeService(schemeProvider, optionsCache, serviceProvider, logger);

        Result<UpstreamProvider> providerResult = UpstreamProvider.Create("google", "Google", "https://accounts.google.com", "client-id");
        providerResult.IsSuccess.ShouldBeTrue();

        // Act
        await service.RegisterSchemeAsync(providerResult.Value);

        // Assert
        schemeProvider.Received(1).AddScheme(Arg.Is<AuthenticationScheme>(s => s!.Name == "google"));
        optionsCache.Received(1).TryAdd("google", Arg.Any<OpenIdConnectOptions>());
    }

    [Fact]
    public async Task RegisterSchemeAsync_WithExistingScheme_RemovesAndReadds()
    {
        // Arrange
        IAuthenticationSchemeProvider schemeProvider = Substitute.For<IAuthenticationSchemeProvider>();
        IOptionsMonitorCache<OpenIdConnectOptions> optionsCache = Substitute.For<IOptionsMonitorCache<OpenIdConnectOptions>>();
        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        ILogger<DynamicAuthenticationSchemeService> logger = Substitute.For<ILogger<DynamicAuthenticationSchemeService>>();

        var existingScheme = new AuthenticationScheme("google", "Google", typeof(OpenIdConnectHandler));
        schemeProvider.GetSchemeAsync("google").Returns(existingScheme);
        serviceProvider.GetService(typeof(IEnumerable<IPostConfigureOptions<OpenIdConnectOptions>>))
            .Returns(Array.Empty<IPostConfigureOptions<OpenIdConnectOptions>>());

        var service = new DynamicAuthenticationSchemeService(schemeProvider, optionsCache, serviceProvider, logger);

        Result<UpstreamProvider> providerResult = UpstreamProvider.Create("google", "Google", "https://accounts.google.com", "client-id");
        providerResult.IsSuccess.ShouldBeTrue();

        // Act
        await service.RegisterSchemeAsync(providerResult.Value);

        // Assert
        schemeProvider.Received(1).RemoveScheme("google");
        optionsCache.Received(1).TryRemove("google");
        schemeProvider.Received(1).AddScheme(Arg.Is<AuthenticationScheme>(s => s!.Name == "google"));
    }

    [Fact]
    public void RemoveScheme_RemovesFromProviderAndCache()
    {
        // Arrange
        IAuthenticationSchemeProvider schemeProvider = Substitute.For<IAuthenticationSchemeProvider>();
        IOptionsMonitorCache<OpenIdConnectOptions> optionsCache = Substitute.For<IOptionsMonitorCache<OpenIdConnectOptions>>();
        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        ILogger<DynamicAuthenticationSchemeService> logger = Substitute.For<ILogger<DynamicAuthenticationSchemeService>>();

        var service = new DynamicAuthenticationSchemeService(schemeProvider, optionsCache, serviceProvider, logger);

        // Act
        service.RemoveScheme("google");

        // Assert
        schemeProvider.Received(1).RemoveScheme("google");
        optionsCache.Received(1).TryRemove("google");
    }
}

/// <summary>
/// Unit tests for ExternalAuthenticationExtensions.
/// </summary>
public sealed class ExternalAuthenticationExtensionsTests
{
    [Fact]
    public void AddExternalCookie_AddsExternalCookieScheme()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new AuthenticationBuilder(services);

        // Act
        AuthenticationBuilder result = builder.AddExternalCookie();

        // Assert
        result.ShouldNotBeNull();
        // The cookie scheme should be registered in the services
        services.ShouldContain(s => s.ServiceType == typeof(IConfigureOptions<CookieAuthenticationOptions>));
    }

    [Fact]
    public void AddDynamicExternalAuthentication_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDynamicExternalAuthentication();

        // Assert
        services.ShouldContain(s => s.ServiceType == typeof(DynamicAuthenticationSchemeService));
        services.ShouldContain(s => s.ServiceType == typeof(IDynamicAuthenticationSchemeService));
        services.ShouldContain(s => s.ServiceType == typeof(IHostedService));
    }
}
