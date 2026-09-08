using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using OpenIddict.Quartz;
using OpenIddict.Server;
using OpenIdentityStack.Infrastructure.Persistence;
using Quartz;

namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>
/// Extension methods for configuring OpenIddict.
/// </summary>
public static class OpenIddictSetup
{
    /// <summary>
    /// Adds and configures OpenIddict services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOpenIddictConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        string environmentName)
    {
        bool isDevelopmentLike =
            string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);

        services.Configure<QuartzOptions>(configuration.GetSection("Quartz"));

        services.AddQuartz(options =>
        {
            options.UseSimpleTypeLoader();
        });

        IConfigurationSection quartzHostedServiceSection = configuration.GetSection("Quartz:HostedService");

        services.AddQuartzHostedService(options =>
        {
            quartzHostedServiceSection.Bind(options);
            options.WaitForJobsToComplete =
                quartzHostedServiceSection.GetValue<bool?>(nameof(QuartzHostedServiceOptions.WaitForJobsToComplete))
                ?? true;
        });

        services.AddOpenIddict()
            // Register the OpenIddict core components
            .AddCore(options =>
            {
                // Configure OpenIddict to use the Entity Framework Core stores and models
                options.UseEntityFrameworkCore()
                    .UseDbContext<OpenIdentityStackDbContext>();

                OpenIddictQuartzBuilder quartzBuilder = options.UseQuartz();
                ConfigureQuartzPruning(quartzBuilder, configuration);
            })

            // Register the OpenIddict server components
            .AddServer(options =>
            {
                options.AddTokenIssuanceTransaction();
                options.AddEventHandler<OpenIddictServerEvents.GenerateTokenContext>(builder =>
                    builder.UseScopedHandler<ApplicationTokenSubjectMetadata>()
                        .SetOrder(OpenIddictServerHandlers.Protection.CreateTokenEntry.Descriptor.Order + 1_000));
                options.AddEventHandler<OpenIddictServerEvents.ValidateTokenContext>(builder =>
                    builder.UseScopedHandler<UserCredentialRevisionValidation>()
                        .SetOrder(OpenIddictServerHandlers.Protection.ValidateAuthorizationEntry.Descriptor.Order + 1_000));
                options.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(builder =>
                    builder.UseScopedHandler<UserCredentialRevisionValidation>()
                        .SetOrder(int.MinValue + 75_000));
                string? configuredIssuer = configuration["OpenIddict:Issuer"];
                Uri? issuer = null;
                if (!string.IsNullOrWhiteSpace(configuredIssuer))
                {
                    issuer = new Uri(configuredIssuer, UriKind.Absolute);
                    options.SetIssuer(issuer);
                }

                // Enable the authorization, token, userinfo, introspection, and revocation endpoints
                if (issuer is not null)
                {
                    options.SetAuthorizationEndpointUris(new Uri(issuer, "connect/authorize"))
                        .SetTokenEndpointUris(new Uri(issuer, "connect/token"))
                        .SetUserInfoEndpointUris(new Uri(issuer, "connect/userinfo"))
                        .SetIntrospectionEndpointUris(new Uri(issuer, "connect/introspect"))
                        .SetRevocationEndpointUris(new Uri(issuer, "connect/revoke"))
                        .SetEndSessionEndpointUris(new Uri(issuer, "connect/logout"))
                        .SetConfigurationEndpointUris(new Uri(issuer, ".well-known/openid-configuration"))
                        .SetJsonWebKeySetEndpointUris(new Uri(issuer, ".well-known/jwks"));
                }
                else
                {
                    options.SetAuthorizationEndpointUris("connect/authorize")
                        .SetTokenEndpointUris("connect/token")
                        .SetUserInfoEndpointUris("connect/userinfo")
                        .SetIntrospectionEndpointUris("connect/introspect")
                        .SetRevocationEndpointUris("connect/revoke")
                        .SetEndSessionEndpointUris("connect/logout")
                        .SetConfigurationEndpointUris(".well-known/openid-configuration")
                        .SetJsonWebKeySetEndpointUris(".well-known/jwks");
                }

                // Enable the authorization code flow. PKCE is enforced per client
                // so confidential certification clients can still exercise the
                // non-PKCE Basic OP conformance path.
                options.AllowAuthorizationCodeFlow();

                // Enable the client credentials flow for service accounts
                options.AllowClientCredentialsFlow();

                // Enable the refresh token flow
                options.AllowRefreshTokenFlow();

                options.RegisterScopes(
                    OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Scopes.Email,
                    OpenIddictConstants.Scopes.Address,
                    OpenIddictConstants.Scopes.Phone,
                    OpenIddictConstants.Scopes.Roles,
                    "api");

                options.RegisterClaims(
                    OpenIddictConstants.Claims.AuthenticationTime,
                    OpenIddictConstants.Claims.Name,
                    OpenIddictConstants.Claims.GivenName,
                    OpenIddictConstants.Claims.FamilyName,
                    OpenIddictConstants.Claims.MiddleName,
                    OpenIddictConstants.Claims.Nickname,
                    OpenIddictConstants.Claims.PreferredUsername,
                    OpenIddictConstants.Claims.Profile,
                    OpenIddictConstants.Claims.Picture,
                    OpenIddictConstants.Claims.Website,
                    OpenIddictConstants.Claims.Gender,
                    OpenIddictConstants.Claims.Birthdate,
                    OpenIddictConstants.Claims.Zoneinfo,
                    OpenIddictConstants.Claims.Locale,
                    OpenIddictConstants.Claims.UpdatedAt,
                    OpenIddictConstants.Claims.Email,
                    OpenIddictConstants.Claims.EmailVerified,
                    OpenIddictConstants.Claims.Address,
                    OpenIddictConstants.Claims.PhoneNumber,
                    OpenIddictConstants.Claims.PhoneNumberVerified);

                string? signingBase64 = configuration["OpenIddict:Certificates:Signing:Base64"];
                string? signingPath = configuration["OpenIddict:Certificates:Signing:Path"];
                string? signingPassword = configuration["OpenIddict:Certificates:Signing:Password"];
                string? signingCertificatePath = configuration["OpenIddict:Certificates:Signing:CertificatePath"];
                string? signingPrivateKeyPath = configuration["OpenIddict:Certificates:Signing:PrivateKeyPath"];
                string? encryptionBase64 = configuration["OpenIddict:Certificates:Encryption:Base64"];
                string? encryptionPath = configuration["OpenIddict:Certificates:Encryption:Path"];
                string? encryptionPassword = configuration["OpenIddict:Certificates:Encryption:Password"];
                string? encryptionCertificatePath = configuration["OpenIddict:Certificates:Encryption:CertificatePath"];
                string? encryptionPrivateKeyPath = configuration["OpenIddict:Certificates:Encryption:PrivateKeyPath"];

                bool hasSigningCertificate = HasCertificateConfiguration(signingBase64, signingPath, signingCertificatePath, signingPrivateKeyPath);
                bool hasEncryptionCertificate = HasCertificateConfiguration(encryptionBase64, encryptionPath, encryptionCertificatePath, encryptionPrivateKeyPath);

                if (hasSigningCertificate && hasEncryptionCertificate)
                {
                    options.AddSigningCertificate(LoadCertificate(signingBase64, signingPath, signingPassword, signingCertificatePath, signingPrivateKeyPath))
                        .AddEncryptionCertificate(LoadCertificate(encryptionBase64, encryptionPath, encryptionPassword, encryptionCertificatePath, encryptionPrivateKeyPath));
                }
                else if (hasSigningCertificate || hasEncryptionCertificate)
                {
                    throw new InvalidOperationException("OpenIddict certificate configuration is incomplete. Configure both signing and encryption certificates.");
                }
                else if (isDevelopmentLike)
                {
                    options.AddDevelopmentEncryptionCertificate()
                        .AddDevelopmentSigningCertificate();
                }
                else
                {
                    throw new InvalidOperationException("OpenIddict signing and encryption certificates must be configured outside Development/Testing.");
                }

                // Register the ASP.NET Core host and configure the authorization endpoint
                // to allow the /authorize minimal API endpoint to handle authorization requests
                OpenIddictServerAspNetCoreBuilder aspNetCoreBuilder = options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough()
                    .EnableStatusCodePagesIntegration();

                if (isDevelopmentLike)
                {
                    aspNetCoreBuilder.DisableTransportSecurityRequirement();
                }

                if (isDevelopmentLike)
                {
                    options.DisableAccessTokenEncryption();
                }

                options.AddApplicationClientAuthentication();
                options.AddEventHandler<OpenIddictServerEvents.ValidateTokenContext>(builder => builder
                    .UseScopedHandler<CredentialBoundaryValidation>()
                    .SetOrder(OpenIddictServerHandlers.Protection.ValidateAuthorizationEntry.Descriptor.Order + 2_000));
                options.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(builder => builder
                    .UseScopedHandler<CredentialBoundaryValidation>().SetOrder(int.MinValue + 100_000));

                // Enrich successful token introspection responses with caller-filtered
                // permission metadata while keeping OpenIddict's client authentication
                // and token activity checks in the built-in endpoint pipeline.
                options.AddEventHandler<OpenIddictServerEvents.HandleIntrospectionRequestContext>(builder =>
                    builder.UseScopedHandler<IntrospectionPermissionsHandler>());

                // Keep OpenIddict's storage identifiers out of public ID tokens.
                options.AddInternalTokenClaimTrimming();

                // Add session management handlers (session_state + check_session_iframe)
                options.AddSessionManagement();
            })

            // Register the OpenIddict validation components
            .AddValidation(options =>
            {
                options.AddEventHandler<OpenIddict.Validation.OpenIddictValidationEvents.ValidateTokenContext>(builder =>
                    builder.UseScopedHandler<UserCredentialRevisionValidation>()
                        .SetOrder(OpenIddict.Validation.OpenIddictValidationHandlers.Protection.ValidateAuthorizationEntry.Descriptor.Order + 1_000));
                // Import the configuration from the local OpenIddict server instance
                options.UseLocalServer();
                options.AddEventHandler<OpenIddict.Validation.OpenIddictValidationEvents.ValidateTokenContext>(builder => builder
                    .UseScopedHandler<CredentialBoundaryValidation>()
                    .SetOrder(OpenIddict.Validation.OpenIddictValidationHandlers.Protection.ValidateAuthorizationEntry.Descriptor.Order + 2_000));

                // Enforce token/authorization entry validation for immediate revocation checks
                options.EnableTokenEntryValidation()
                    .EnableAuthorizationEntryValidation();

                // Register the ASP.NET Core host
                options.UseAspNetCore();
            });

        // Advertise and accept S256 only. With "plain" the verifier *is* the challenge,
        // so it protects against nothing, and RFC 7636 section 7.2 requires any client
        // capable of S256 to use it. OpenIddict announces both methods by default and
        // exposes no builder API for this, so the set is trimmed directly. Registered
        // after the AddOpenIddict chain so it runs last and cannot be undone by
        // OpenIddict's own post-configuration.
        services.PostConfigure<OpenIddictServerOptions>(options =>
            options.CodeChallengeMethods.Remove(OpenIddictConstants.CodeChallengeMethods.Plain));

        return services;
    }

    private static void ConfigureQuartzPruning(OpenIddictQuartzBuilder quartzBuilder, IConfiguration configuration)
    {
        if (configuration.GetValue<bool?>("OpenIddict:Quartz:DisableAuthorizationPruning") is true)
        {
            quartzBuilder.DisableAuthorizationPruning();
        }

        if (configuration.GetValue<bool?>("OpenIddict:Quartz:DisableTokenPruning") is true)
        {
            quartzBuilder.DisableTokenPruning();
        }

        TimeSpan? minimumAuthorizationLifespan =
            configuration.GetValue<TimeSpan?>("OpenIddict:Quartz:MinimumAuthorizationLifespan");

        if (minimumAuthorizationLifespan is not null)
        {
            quartzBuilder.SetMinimumAuthorizationLifespan(minimumAuthorizationLifespan.Value);
        }

        TimeSpan? minimumTokenLifespan =
            configuration.GetValue<TimeSpan?>("OpenIddict:Quartz:MinimumTokenLifespan");

        if (minimumTokenLifespan is not null)
        {
            quartzBuilder.SetMinimumTokenLifespan(minimumTokenLifespan.Value);
        }

        int? maximumRefireCount = configuration.GetValue<int?>("OpenIddict:Quartz:MaximumRefireCount");

        if (maximumRefireCount is not null)
        {
            quartzBuilder.SetMaximumRefireCount(maximumRefireCount.Value);
        }
    }

    private static bool HasCertificateConfiguration(string? base64, string? pkcs12Path, string? certificatePath, string? privateKeyPath)
    {
        bool hasBase64 = !string.IsNullOrWhiteSpace(base64);
        bool hasPkcs12 = !string.IsNullOrWhiteSpace(pkcs12Path);
        bool hasPemPair = !string.IsNullOrWhiteSpace(certificatePath) && !string.IsNullOrWhiteSpace(privateKeyPath);

        int configuredCount = (hasBase64 ? 1 : 0) + (hasPkcs12 ? 1 : 0) + (hasPemPair ? 1 : 0);
        if (configuredCount > 1)
        {
            throw new InvalidOperationException("Configure only one of: Base64, PKCS#12 path, or PEM certificate/private key pair for each OpenIddict certificate.");
        }

        if (!string.IsNullOrWhiteSpace(certificatePath) ^ !string.IsNullOrWhiteSpace(privateKeyPath))
        {
            throw new InvalidOperationException("PEM certificate configuration is incomplete. Configure both certificate and private key paths.");
        }

        return hasBase64 || hasPkcs12 || hasPemPair;
    }

    private static X509Certificate2 LoadCertificate(
        string? base64,
        string? pkcs12Path,
        string? password,
        string? certificatePath,
        string? privateKeyPath)
    {
        if (!string.IsNullOrWhiteSpace(base64))
        {
            return X509CertificateLoader.LoadPkcs12(
                Convert.FromBase64String(base64),
                password,
                X509KeyStorageFlags.EphemeralKeySet);
        }

        if (!string.IsNullOrWhiteSpace(pkcs12Path))
        {
            if (!File.Exists(pkcs12Path))
            {
                throw new InvalidOperationException($"OpenIddict certificate file '{pkcs12Path}' was not found.");
            }

            X509Certificate2 pkcs12Certificate = X509CertificateLoader.LoadPkcs12FromFile(
                pkcs12Path,
                password,
                X509KeyStorageFlags.EphemeralKeySet);

            if (!pkcs12Certificate.HasPrivateKey)
            {
                throw new InvalidOperationException($"OpenIddict certificate file '{pkcs12Path}' does not contain a certificate with a private key.");
            }

            return pkcs12Certificate;
        }

        if (string.IsNullOrWhiteSpace(certificatePath) || string.IsNullOrWhiteSpace(privateKeyPath))
        {
            throw new InvalidOperationException("OpenIddict certificate configuration is missing.");
        }

        if (!File.Exists(certificatePath))
        {
            throw new InvalidOperationException($"OpenIddict PEM certificate file '{certificatePath}' was not found.");
        }

        if (!File.Exists(privateKeyPath))
        {
            throw new InvalidOperationException($"OpenIddict PEM private key file '{privateKeyPath}' was not found.");
        }

        using var pemCertificate = X509Certificate2.CreateFromPemFile(certificatePath, privateKeyPath);
        X509Certificate2 normalizedCertificate = X509CertificateLoader.LoadPkcs12(
            pemCertificate.Export(X509ContentType.Pkcs12),
            password: null,
            X509KeyStorageFlags.EphemeralKeySet);

        if (!normalizedCertificate.HasPrivateKey)
        {
            throw new InvalidOperationException($"OpenIddict PEM certificate '{certificatePath}' does not contain a private key.");
        }

        return normalizedCertificate;
    }
}
