using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;

namespace OpenIdentityStack.Api.Authentication;

/// <summary>
/// Interface for dynamically registering external authentication schemes.
/// </summary>
public interface IDynamicAuthenticationSchemeService
{
    /// <summary>
    /// Registers an external authentication scheme for an upstream provider.
    /// </summary>
    Task RegisterSchemeAsync(UpstreamProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an external authentication scheme.
    /// </summary>
    void RemoveScheme(string schemeName);
}

/// <summary>
/// Service for dynamically registering external authentication schemes.
/// </summary>
public sealed partial class DynamicAuthenticationSchemeService : IDynamicAuthenticationSchemeService
{
    private readonly IAuthenticationSchemeProvider schemeProvider;
    private readonly IOptionsMonitorCache<OpenIdConnectOptions> optionsCache;
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<DynamicAuthenticationSchemeService> logger;
    private readonly ISecretProtector secretProtector;

    public DynamicAuthenticationSchemeService(
        IAuthenticationSchemeProvider schemeProvider,
        IOptionsMonitorCache<OpenIdConnectOptions> optionsCache,
        IServiceProvider serviceProvider,
        ILogger<DynamicAuthenticationSchemeService> logger,
        ISecretProtector? secretProtector = null)
    {
        this.schemeProvider = schemeProvider;
        this.optionsCache = optionsCache;
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        this.secretProtector = secretProtector ?? new NoopSecretProtector();
    }

    /// <summary>
    /// Registers an external authentication scheme for an upstream provider.
    /// </summary>
    public async Task RegisterSchemeAsync(UpstreamProvider provider, CancellationToken cancellationToken = default)
    {
        string schemeName = provider.Name;

        // Check if scheme already exists
        AuthenticationScheme? existingScheme = await this.schemeProvider.GetSchemeAsync(schemeName);
        if (existingScheme is not null)
        {
            this.LogSchemeAlreadyRegistered(schemeName);
            // Remove and re-add to update
            this.schemeProvider.RemoveScheme(schemeName);
            this.optionsCache.TryRemove(schemeName);
        }

        // Configure the OpenIdConnect options
        var options = new OpenIdConnectOptions
        {
            Authority = provider.Authority,
            ClientId = provider.ClientId,
            ClientSecret = this.secretProtector.Unprotect(provider.ClientSecret),
            ResponseType = OpenIdConnectResponseType.Code,
            SaveTokens = true,
            GetClaimsFromUserInfoEndpoint = true,
            CallbackPath = $"/signin-{schemeName}",
            SignedOutCallbackPath = $"/signout-{schemeName}",
            SignInScheme = "ExternalCookie",
            Events = new OpenIdConnectEvents
            {
                // Pass prompt=login to upstream IdP when requested
                OnRedirectToIdentityProvider = context =>
                {
                    // Check if prompt was set in the authentication properties
                    if (context.Properties.Items.TryGetValue("prompt", out string? prompt) && !string.IsNullOrEmpty(prompt))
                    {
                        context.ProtocolMessage.Prompt = prompt;
                    }

                    return Task.CompletedTask;
                }
            }
        };

        // Clear default scopes and add required ones
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");

        // Run the post-configuration to properly initialize the options
        // This is required for OpenIdConnect to work properly (sets up nonce cookie, etc.)
        IEnumerable<IPostConfigureOptions<OpenIdConnectOptions>> postConfigures = 
            this.serviceProvider.GetServices<IPostConfigureOptions<OpenIdConnectOptions>>();
        foreach (IPostConfigureOptions<OpenIdConnectOptions> postConfigure in postConfigures)
        {
            postConfigure.PostConfigure(schemeName, options);
        }

        // Add options to the cache
        this.optionsCache.TryAdd(schemeName, options);

        // Register the authentication scheme
        var scheme = new AuthenticationScheme(
            schemeName,
            provider.DisplayName,
            typeof(OpenIdConnectHandler));

        this.schemeProvider.AddScheme(scheme);

        this.LogSchemeRegistered(schemeName, provider.DisplayName);
    }

    /// <summary>
    /// Removes an external authentication scheme.
    /// </summary>
    public void RemoveScheme(string schemeName)
    {
        this.schemeProvider.RemoveScheme(schemeName);
        this.optionsCache.TryRemove(schemeName);
        this.LogSchemeRemoved(schemeName);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Authentication scheme '{SchemeName}' already registered, updating options")]
    private partial void LogSchemeAlreadyRegistered(string schemeName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Registered external authentication scheme '{SchemeName}' for provider '{DisplayName}'")]
    private partial void LogSchemeRegistered(string schemeName, string displayName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed external authentication scheme '{SchemeName}'")]
    private partial void LogSchemeRemoved(string schemeName);

    private sealed class NoopSecretProtector : ISecretProtector
    {
        public string Protect(string secret) => secret;

        public string? Unprotect(string? protectedSecret) => protectedSecret;
    }
}

/// <summary>
/// Hosted service that configures external authentication providers on startup.
/// </summary>
public sealed partial class ExternalAuthenticationStartupService : IHostedService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<ExternalAuthenticationStartupService> logger;

    public ExternalAuthenticationStartupService(
        IServiceProvider serviceProvider,
        ILogger<ExternalAuthenticationStartupService> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        this.LogConfiguringProviders();

        using IServiceScope scope = this.serviceProvider.CreateScope();
        IUpstreamProviderRepository providerRepository = scope.ServiceProvider.GetRequiredService<IUpstreamProviderRepository>();
        DynamicAuthenticationSchemeService schemeService = scope.ServiceProvider.GetRequiredService<DynamicAuthenticationSchemeService>();

        try
        {
            IReadOnlyList<UpstreamProvider> providers = await providerRepository.GetActiveProvidersAsync(cancellationToken);

            foreach (UpstreamProvider provider in providers)
            {
                await schemeService.RegisterSchemeAsync(provider, cancellationToken);
            }

            this.LogProvidersConfigured(providers.Count);
        }
        catch (Exception ex)
        {
            this.LogConfigurationFailed(ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Configuring external authentication providers...")]
    private partial void LogConfiguringProviders();

    [LoggerMessage(Level = LogLevel.Information, Message = "Configured {Count} external authentication provider(s)")]
    private partial void LogProvidersConfigured(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to configure external authentication providers. External login will not be available.")]
    private partial void LogConfigurationFailed(Exception ex);
}

/// <summary>
/// Extension methods for external authentication.
/// </summary>
public static class ExternalAuthenticationExtensions
{
    /// <summary>
    /// Adds the external cookie scheme used to temporarily store external identity.
    /// This cookie is used during the OAuth callback flow before the user is signed in locally.
    /// </summary>
    public static AuthenticationBuilder AddExternalCookie(
        this AuthenticationBuilder builder,
        IHostEnvironment environment)
    {
        return builder.AddCookie("ExternalCookie", options =>
        {
            options.Cookie.Name = ".OpenIdentityStack.External";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = environment.IsDevelopment() || environment.IsEnvironment("Testing")
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(5); // Short-lived for security
        });
    }

    public static AuthenticationBuilder AddExternalCookie(this AuthenticationBuilder builder)
    {
        return builder.AddCookie("ExternalCookie", options =>
        {
            options.Cookie.Name = ".OpenIdentityStack.External";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        });
    }

    /// <summary>
    /// Adds the dynamic external authentication services.
    /// </summary>
    public static IServiceCollection AddDynamicExternalAuthentication(this IServiceCollection services)
    {
        // Register the OpenIdConnect post-configure services needed for dynamic scheme creation
        // This doesn't add any actual schemes, just the infrastructure needed to configure them dynamically
        services.AddSingleton<IPostConfigureOptions<OpenIdConnectOptions>, OpenIdConnectPostConfigureOptions>();
        
        services.AddSingleton<DynamicAuthenticationSchemeService>();
        services.AddSingleton<IDynamicAuthenticationSchemeService>(sp => sp.GetRequiredService<DynamicAuthenticationSchemeService>());
        services.AddHostedService<ExternalAuthenticationStartupService>();
        return services;
    }
}
