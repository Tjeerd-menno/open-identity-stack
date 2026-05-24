using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIdentityStack.Application.Applications.Commands;
using SharedKernel;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace OpenIdentityStack.Infrastructure.Identity;

public sealed class ApplicationClientAuthenticationHandler : IOpenIddictServerHandler<ValidateTokenRequestContext>
{
    private readonly IValidateApplicationClientCredentialsUseCase validateCredentialsUseCase;
    private readonly IValidateApplicationCertificateUseCase validateCertificateUseCase;

    public ApplicationClientAuthenticationHandler(
        IValidateApplicationClientCredentialsUseCase validateCredentialsUseCase,
        IValidateApplicationCertificateUseCase validateCertificateUseCase)
    {
        this.validateCredentialsUseCase = validateCredentialsUseCase;
        this.validateCertificateUseCase = validateCertificateUseCase;
    }

    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateTokenRequestContext>()
            .AddFilter<RequireClientCredentialsGrantType>()
            .UseScopedHandler<ApplicationClientAuthenticationHandler>()
            .SetOrder(OpenIddictServerHandlers.Exchange.ValidateClientCredentialsParameters.Descriptor.Order + 1000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(ValidateTokenRequestContext context)
    {
        if (context.Request is null || !context.Request.IsClientCredentialsGrantType())
        {
            return;
        }

        string? clientId = context.Request.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return;
        }

        string? clientSecret = context.Request.ClientSecret;
        if (!string.IsNullOrEmpty(clientSecret))
        {
            var command = new ValidateApplicationClientCredentialsCommand(clientId, clientSecret);
            Result<ValidateApplicationCredentialsResult> result =
                await this.validateCredentialsUseCase.ExecuteAsync(command, context.CancellationToken);

            if (result.IsFailure)
            {
                RejectInvalidClient(context);
            }

            return;
        }

        X509Certificate2? clientCertificate = context.Transaction
            .GetProperty<HttpContext>(typeof(HttpContext).FullName!)
            ?.Connection.ClientCertificate;
        if (clientCertificate is null)
        {
            return;
        }

        string thumbprint = clientCertificate.GetCertHashString(HashAlgorithmName.SHA256);
        var certificateCommand = new ValidateApplicationCertificateCommand(clientId, thumbprint);
        Result<ValidateApplicationCredentialsResult> validationResult =
            await this.validateCertificateUseCase.ExecuteAsync(certificateCommand, context.CancellationToken);

        if (validationResult.IsFailure)
        {
            RejectInvalidClient(context);
        }
    }

    private static void RejectInvalidClient(ValidateTokenRequestContext context) =>
        context.Reject(
            error: Errors.InvalidClient,
            description: "The client credentials are invalid.");

    public sealed class RequireClientCredentialsGrantType : IOpenIddictServerHandlerFilter<ValidateTokenRequestContext>
    {
        public ValueTask<bool> IsActiveAsync(ValidateTokenRequestContext context) =>
            new(context.Request?.IsClientCredentialsGrantType() == true);
    }
}

public static class ApplicationClientAuthenticationExtensions
{
    public static OpenIddictServerBuilder AddApplicationClientAuthentication(this OpenIddictServerBuilder builder)
    {
        builder.Services.AddScoped<ApplicationClientAuthenticationHandler>();
        builder.Services.AddScoped<ApplicationClientAuthenticationHandler.RequireClientCredentialsGrantType>();
        builder.AddEventHandler(ApplicationClientAuthenticationHandler.Descriptor);
        return builder;
    }
}
