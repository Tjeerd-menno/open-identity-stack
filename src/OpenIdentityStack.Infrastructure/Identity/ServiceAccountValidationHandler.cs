using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ServiceAccounts.Commands;
using static OpenIddict.Server.OpenIddictServerEvents;
using static OpenIddict.Abstractions.OpenIddictConstants;
using OpenIdentityStack.Domain.ServiceAccounts;
using OpenIdentityStack.Domain.Common;
using System.Security.Cryptography.X509Certificates;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>
/// OpenIddict event handler that validates client credentials against our ServiceAccount store.
/// This handler runs after OpenIddict's built-in client validation and adds custom business logic.
/// </summary>
public sealed class ServiceAccountValidationHandler : IOpenIddictServerHandler<ValidateTokenRequestContext>
{
    private readonly IServiceAccountRepository serviceAccountRepository;
    private readonly IValidateClientCredentialsUseCase validateCredentialsUseCase;
    private readonly IValidateCertificateUseCase validateCertificateUseCase;

    public ServiceAccountValidationHandler(
        IServiceAccountRepository serviceAccountRepository,
        IValidateClientCredentialsUseCase validateCredentialsUseCase,
        IValidateCertificateUseCase validateCertificateUseCase)
    {
        this.serviceAccountRepository = serviceAccountRepository;
        this.validateCredentialsUseCase = validateCredentialsUseCase;
        this.validateCertificateUseCase = validateCertificateUseCase;
    }

    /// <summary>
    /// Gets the default descriptor for this handler.
    /// Runs after the built-in OpenIddict handlers for client authentication.
    /// </summary>
    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateTokenRequestContext>()
            .AddFilter<RequireClientCredentialsGrantType>()
            .UseScopedHandler<ServiceAccountValidationHandler>()
            .SetOrder(OpenIddictServerHandlers.Exchange.ValidateClientCredentialsParameters.Descriptor.Order + 1000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    /// <inheritdoc/>
    public async ValueTask HandleAsync(ValidateTokenRequestContext context)
    {
        if (context.Request is null || !context.Request.IsClientCredentialsGrantType())
        {
            return;
        }

        string? clientId = context.Request.ClientId;
        if (string.IsNullOrEmpty(clientId))
        {
            return; // OpenIddict will handle this error
        }

        // Check if we have a ServiceAccount for this client
        ServiceAccount? serviceAccount = await this.serviceAccountRepository.GetByClientIdAsync(clientId, context.CancellationToken);
        if (serviceAccount is null)
        {
            // No ServiceAccount found - let OpenIddict's standard validation handle it
            // This allows using OpenIddict's built-in application store for other clients
            return;
        }

        // Validate using our ServiceAccount domain logic
        string? clientSecret = context.Request.ClientSecret;
        if (!string.IsNullOrEmpty(clientSecret))
        {
            // Validate with client secret
            var command = new ValidateClientCredentialsCommand(clientId, clientSecret);
            Result<ValidateClientCredentialsResult> result = await this.validateCredentialsUseCase.ExecuteAsync(command, context.CancellationToken);

            if (result.IsFailure)
            {
                context.Reject(
                    error: Errors.InvalidClient,
                    description: "The client credentials are invalid.");
                return;
            }
        }
        else
        {
            // Check for certificate-based authentication
            // Note: This requires mTLS to be configured and the client certificate to be present
            // OpenIddict stores the HttpContext in the transaction properties when using ASP.NET Core
            if (context.Transaction.GetProperty<HttpContext>(typeof(HttpContext).FullName!) is { } httpContext)
            {
                X509Certificate2? clientCertificate = httpContext.Connection?.ClientCertificate;

                if (clientCertificate is not null)
                {
                    string thumbprint = clientCertificate.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256);
                    var command = new ValidateCertificateCommand(clientId, thumbprint);
                    Result<ValidateCertificateResult> validationResult = await this.validateCertificateUseCase.ExecuteAsync(command, context.CancellationToken);

                    if (validationResult.IsFailure)
                    {
                        context.Reject(
                            error: Errors.InvalidClient,
                            description: "The client certificate is invalid.");
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Filter that ensures this handler only runs for client credentials grant type.
    /// </summary>
    public sealed class RequireClientCredentialsGrantType : IOpenIddictServerHandlerFilter<ValidateTokenRequestContext>
    {
        public ValueTask<bool> IsActiveAsync(ValidateTokenRequestContext context)
        {
            return new ValueTask<bool>(context.Request?.IsClientCredentialsGrantType() == true);
        }
    }
}

/// <summary>
/// Extension methods for registering ServiceAccount validation with OpenIddict.
/// </summary>
public static class ServiceAccountValidationExtensions
{
    /// <summary>
    /// Adds ServiceAccount validation to OpenIddict server.
    /// </summary>
    public static OpenIddictServerBuilder AddServiceAccountValidation(this OpenIddictServerBuilder builder)
    {
        // Register the implementation types in DI so OpenIddict can resolve them
        builder.Services.AddScoped<ServiceAccountValidationHandler>();
        builder.Services.AddScoped<ServiceAccountValidationHandler.RequireClientCredentialsGrantType>();

        builder.AddEventHandler(ServiceAccountValidationHandler.Descriptor);
        return builder;
    }
}
