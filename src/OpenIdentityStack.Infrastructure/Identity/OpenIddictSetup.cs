using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIdentityStack.Infrastructure.Persistence;

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

        services.AddOpenIddict()
            // Register the OpenIddict core components
            .AddCore(options =>
            {
                // Configure OpenIddict to use the Entity Framework Core stores and models
                options.UseEntityFrameworkCore()
                    .UseDbContext<OpenIdentityStackDbContext>();
            })

            // Register the OpenIddict server components
            .AddServer(options =>
            {
                // Enable the authorization, token, userinfo, introspection, and revocation endpoints
                options.SetAuthorizationEndpointUris("connect/authorize")
                    .SetTokenEndpointUris("connect/token")
                    .SetUserInfoEndpointUris("connect/userinfo")
                    .SetIntrospectionEndpointUris("connect/introspect")
                    .SetRevocationEndpointUris("connect/revoke")
                    .SetEndSessionEndpointUris("connect/logout")
                    .SetConfigurationEndpointUris(".well-known/openid-configuration")
                    .SetJsonWebKeySetEndpointUris(".well-known/jwks");

                // Enable the authorization code flow with PKCE
                options.AllowAuthorizationCodeFlow()
                    .RequireProofKeyForCodeExchange();

                // Enable the client credentials flow for service accounts
                options.AllowClientCredentialsFlow();

                // Enable the refresh token flow
                options.AllowRefreshTokenFlow();

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

                // Force client applications to use Proof Key for Code Exchange (PKCE)
                options.RequireProofKeyForCodeExchange();

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

                // Add ServiceAccount validation handler for custom client validation
                options.AddServiceAccountValidation();

                // Add session management handlers (session_state + check_session_iframe)
                options.AddSessionManagement();
            })

            // Register the OpenIddict validation components
            .AddValidation(options =>
            {
                // Import the configuration from the local OpenIddict server instance
                options.UseLocalServer();

                // Enforce token/authorization entry validation for immediate revocation checks
                options.EnableTokenEntryValidation()
                    .EnableAuthorizationEntryValidation();

                // Register the ASP.NET Core host
                options.UseAspNetCore();
            });

        return services;
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
