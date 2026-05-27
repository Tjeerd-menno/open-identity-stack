using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Application.ServiceAccounts.Commands;
using OpenIdentityStack.Domain.Applications;
using SharedKernel;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Tests.ServiceAccounts;

public sealed class ValidateCertificateUseCaseTests
{
    private readonly IValidateApplicationCertificateUseCase applicationValidationUseCase;
    private readonly ValidateCertificateUseCase useCase;

    public ValidateCertificateUseCaseTests()
    {
        this.applicationValidationUseCase = Substitute.For<IValidateApplicationCertificateUseCase>();
        this.useCase = new ValidateCertificateUseCase(this.applicationValidationUseCase);
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesToUnifiedApplicationCertificateValidation()
    {
        var applicationId = new DomainApplicationId(Guid.NewGuid());
        this.applicationValidationUseCase
            .ExecuteAsync(
                Arg.Is<ValidateApplicationCertificateCommand>(command =>
                    command.ClientId == "worker" && command.CertificateThumbprint == "ABC123"),
                Arg.Any<CancellationToken>())
            .Returns(new ValidateApplicationCredentialsResult(
                applicationId,
                "worker",
                "Worker",
                ["api"],
                ["client_credentials"]));

        Result<ValidateCertificateResult> result = await this.useCase.ExecuteAsync(
            new ValidateCertificateCommand("worker", "ABC123"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ServiceAccountId.Value.ShouldBe(applicationId.Value);
        result.Value.ClientId.ShouldBe("worker");
        result.Value.DisplayName.ShouldBe("Worker");
        result.Value.AllowedScopes.ShouldBe(["api"]);
        result.Value.AllowedGrantTypes.ShouldBe(["client_credentials"]);
    }

    [Theory]
    [InlineData("Application.ClientIdRequired", "ServiceAccount.ClientIdRequired")]
    [InlineData("Application.Disabled", "ServiceAccount.AccountDisabled")]
    [InlineData("Application.InvalidCredentials", "ServiceAccount.InvalidCredentials")]
    public async Task ExecuteAsync_MapsUnifiedApplicationValidationErrorsToLegacyServiceAccountErrors(
        string applicationErrorCode,
        string expectedLegacyErrorCode)
    {
        this.applicationValidationUseCase
            .ExecuteAsync(Arg.Any<ValidateApplicationCertificateCommand>(), Arg.Any<CancellationToken>())
            .Returns(DomainError.Unauthorized(applicationErrorCode, "Validation failed."));

        Result<ValidateCertificateResult> result = await this.useCase.ExecuteAsync(
            new ValidateCertificateCommand("worker", "ABC123"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(expectedLegacyErrorCode);
    }
}
