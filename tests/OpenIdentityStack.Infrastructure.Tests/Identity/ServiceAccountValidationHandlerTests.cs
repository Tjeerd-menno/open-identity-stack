
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ServiceAccounts.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServiceAccounts;
using OpenIdentityStack.Infrastructure.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Tests.Identity;
public sealed class ServiceAccountValidationHandlerTests
{
    private readonly IServiceAccountRepository serviceAccountRepository;
    private readonly IValidateClientCredentialsUseCase validateCredentialsUseCase;
    private readonly IValidateCertificateUseCase validateCertificateUseCase;
    private readonly ServiceAccountValidationHandler handler;

    public ServiceAccountValidationHandlerTests()
    {
        this.serviceAccountRepository = Substitute.For<IServiceAccountRepository>();
        this.validateCredentialsUseCase = Substitute.For<IValidateClientCredentialsUseCase>();
        this.validateCertificateUseCase = Substitute.For<IValidateCertificateUseCase>();
        this.handler = new ServiceAccountValidationHandler(
            this.serviceAccountRepository,
            this.validateCredentialsUseCase,
            this.validateCertificateUseCase);
    }

    [Fact]
    public async Task HandleAsync_ShouldIgnore_WhenNotClientCredentialsGrant()
    {
        // Arrange
        ValidateTokenRequestContext context = CreateContext(new OpenIddictRequest
        {
            GrantType = GrantTypes.AuthorizationCode,
            ClientId = "client"
        });

        // Act
        await this.handler.HandleAsync(context);

        // Assert
        await this.serviceAccountRepository.DidNotReceive()
            .GetByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldIgnore_WhenClientIdMissing()
    {
        // Arrange
        ValidateTokenRequestContext context = CreateContext(new OpenIddictRequest
        {
            GrantType = GrantTypes.ClientCredentials
        });

        // Act
        await this.handler.HandleAsync(context);

        // Assert
        await this.serviceAccountRepository.DidNotReceive()
            .GetByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldIgnore_WhenServiceAccountNotFound()
    {
        // Arrange
        ValidateTokenRequestContext context = CreateContext(new OpenIddictRequest
        {
            GrantType = GrantTypes.ClientCredentials,
            ClientId = "client",
            ClientSecret = "secret"
        });

        this.serviceAccountRepository.GetByClientIdAsync("client", Arg.Any<CancellationToken>())
            .Returns((ServiceAccount?)null);

        // Act
        await this.handler.HandleAsync(context);

        // Assert
        await this.validateCredentialsUseCase.DidNotReceive()
            .ExecuteAsync(Arg.Any<ValidateClientCredentialsCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldReject_WhenCredentialsInvalid()
    {
        // Arrange
        ValidateTokenRequestContext context = CreateContext(new OpenIddictRequest
        {
            GrantType = GrantTypes.ClientCredentials,
            ClientId = "client",
            ClientSecret = "secret"
        });

        ServiceAccount serviceAccount = CreateServiceAccount();

        this.serviceAccountRepository.GetByClientIdAsync("client", Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        this.validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateClientCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(DomainError.Validation("Invalid", "Invalid"));

        // Act
        await this.handler.HandleAsync(context);
        // Assert
        context.IsRejected.ShouldBeTrue();
        context.Error.ShouldBe(Errors.InvalidClient);
    }

    [Fact]
    public async Task HandleAsync_ShouldNotReject_WhenCredentialsValid()
    {
        // Arrange
        ValidateTokenRequestContext context = CreateContext(new OpenIddictRequest
        {
            GrantType = GrantTypes.ClientCredentials,
            ClientId = "client",
            ClientSecret = "secret"
        });

        ServiceAccount serviceAccount = CreateServiceAccount();

        this.serviceAccountRepository.GetByClientIdAsync("client", Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        this.validateCredentialsUseCase.ExecuteAsync(Arg.Any<ValidateClientCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateClientCredentialsResult(
                ServiceAccountId.Create(),
                "client",
                "Client",
                new List<string> { "api" },
                new List<string> { "client_credentials" }));

        // Act
        await this.handler.HandleAsync(context);

        // Assert
        context.IsRejected.ShouldBeFalse();
    }

    [Fact]
    public async Task Filter_ShouldBeActive_ForClientCredentialsGrant()
    {
        var filter = new ServiceAccountValidationHandler.RequireClientCredentialsGrantType();
        ValidateTokenRequestContext context = CreateContext(new OpenIddictRequest
        {
            GrantType = GrantTypes.ClientCredentials,
            ClientId = "client"
        });

        bool isActive = await filter.IsActiveAsync(context);

        isActive.ShouldBeTrue();
    }

    private static ValidateTokenRequestContext CreateContext(OpenIddictRequest request)
    {
        var transaction = new OpenIddictServerTransaction
        {
            Request = request
        };

        transaction.Properties[typeof(HttpContext).FullName!] = new DefaultHttpContext();

        return new ValidateTokenRequestContext(transaction);
    }

    private static ServiceAccount CreateServiceAccount()
    {
        IDateTimeProvider timeProvider = Substitute.For<IDateTimeProvider>();
        timeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);

        return ServiceAccount.Create(
            "client",
            "Client",
            new List<string> { "api" },
            new List<string> { "client_credentials" },
            timeProvider).Value;
    }
}
