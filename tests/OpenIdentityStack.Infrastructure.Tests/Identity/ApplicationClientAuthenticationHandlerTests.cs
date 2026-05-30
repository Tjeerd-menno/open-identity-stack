using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Infrastructure.Identity;
using SharedKernel;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Infrastructure.Tests.Identity;

public sealed class ApplicationClientAuthenticationHandlerTests
{
    private readonly IValidateApplicationClientCredentialsUseCase validateCredentialsUseCase;
    private readonly IValidateApplicationCertificateUseCase validateCertificateUseCase;
    private readonly ApplicationClientAuthenticationHandler handler;

    public ApplicationClientAuthenticationHandlerTests()
    {
        this.validateCredentialsUseCase = Substitute.For<IValidateApplicationClientCredentialsUseCase>();
        this.validateCertificateUseCase = Substitute.For<IValidateApplicationCertificateUseCase>();
        this.handler = new ApplicationClientAuthenticationHandler(
            this.validateCredentialsUseCase,
            this.validateCertificateUseCase);
    }

    [Fact]
    public async Task HandleAsync_IgnoresNonClientCredentialsGrant()
    {
        ValidateTokenRequestContext context = CreateContext(new OpenIddictRequest
        {
            GrantType = GrantTypes.AuthorizationCode,
            ClientId = "client"
        });

        await this.handler.HandleAsync(context);

        await this.validateCredentialsUseCase.DidNotReceive()
            .ExecuteAsync(Arg.Any<ValidateApplicationClientCredentialsCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithValidSecret_DoesNotRejectAndUsesApplicationValidation()
    {
        ValidateTokenRequestContext context = CreateContext(new OpenIddictRequest
        {
            GrantType = GrantTypes.ClientCredentials,
            ClientId = "worker",
            ClientSecret = "secret"
        });
        this.validateCredentialsUseCase.ExecuteAsync(
                Arg.Is<ValidateApplicationClientCredentialsCommand>(command =>
                    command.ClientId == "worker" && command.ClientSecret == "secret"),
                Arg.Any<CancellationToken>())
            .Returns(CreateValidationResult());

        await this.handler.HandleAsync(context);

        context.IsRejected.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Application.Disabled")]
    [InlineData("Application.InvalidCredentials")]
    public async Task HandleAsync_WithRejectedSecretCredential_RejectsInvalidClient(string errorCode)
    {
        ValidateTokenRequestContext context = CreateContext(new OpenIddictRequest
        {
            GrantType = GrantTypes.ClientCredentials,
            ClientId = "worker",
            ClientSecret = "secret"
        });
        this.validateCredentialsUseCase.ExecuteAsync(
                Arg.Any<ValidateApplicationClientCredentialsCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(DomainError.Validation(errorCode, "Invalid application credentials."));

        await this.handler.HandleAsync(context);

        context.IsRejected.ShouldBeTrue();
        context.Error.ShouldBe(Errors.InvalidClient);
    }

    [Fact]
    public async Task HandleAsync_WithClientCertificate_UsesCertificateValidation()
    {
        using X509Certificate2 certificate = CreateCertificate();
        ValidateTokenRequestContext context = CreateContext(new OpenIddictRequest
        {
            GrantType = GrantTypes.ClientCredentials,
            ClientId = "worker"
        }, certificate);
        string thumbprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
        this.validateCertificateUseCase.ExecuteAsync(
                Arg.Is<ValidateApplicationCertificateCommand>(command =>
                    command.ClientId == "worker" && command.CertificateThumbprint == thumbprint),
                Arg.Any<CancellationToken>())
            .Returns(CreateValidationResult());

        await this.handler.HandleAsync(context);

        context.IsRejected.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Application.InvalidCredentials")]
    [InlineData("Application.CredentialNotActive")]
    public async Task HandleAsync_WithRejectedCertificateCredential_RejectsInvalidClient(string errorCode)
    {
        using X509Certificate2 certificate = CreateCertificate();
        ValidateTokenRequestContext context = CreateContext(new OpenIddictRequest
        {
            GrantType = GrantTypes.ClientCredentials,
            ClientId = "worker"
        }, certificate);
        this.validateCertificateUseCase.ExecuteAsync(
                Arg.Any<ValidateApplicationCertificateCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(DomainError.Validation(errorCode, "Invalid application certificate."));

        await this.handler.HandleAsync(context);

        context.IsRejected.ShouldBeTrue();
        context.Error.ShouldBe(Errors.InvalidClient);
    }

    [Fact]
    public async Task Filter_IsActiveForClientCredentialsGrant()
    {
        var filter = new ApplicationClientAuthenticationHandler.RequireClientCredentialsGrantType();
        ValidateTokenRequestContext context = CreateContext(new OpenIddictRequest
        {
            GrantType = GrantTypes.ClientCredentials,
            ClientId = "worker"
        });

        bool isActive = await filter.IsActiveAsync(context);

        isActive.ShouldBeTrue();
    }

    private static ValidateTokenRequestContext CreateContext(
        OpenIddictRequest request,
        X509Certificate2? clientCertificate = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.ClientCertificate = clientCertificate;
        var transaction = new OpenIddictServerTransaction
        {
            Request = request
        };

        transaction.Properties[typeof(HttpContext).FullName!] = httpContext;

        return new ValidateTokenRequestContext(transaction);
    }

    private static Result<ValidateApplicationCredentialsResult> CreateValidationResult() =>
        new ValidateApplicationCredentialsResult(
            DomainApplicationId.NewId(),
            "worker",
            "Worker",
            ["api"],
            ["client_credentials"]);

    private static X509Certificate2 CreateCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=worker", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
