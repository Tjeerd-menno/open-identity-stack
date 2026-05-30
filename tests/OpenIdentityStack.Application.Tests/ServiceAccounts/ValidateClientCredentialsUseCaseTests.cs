using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Application.ServiceAccounts.Commands;
using OpenIdentityStack.Domain.Applications;
using SharedKernel;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Tests.ServiceAccounts;

public sealed class ValidateClientCredentialsUseCaseTests
{
    private readonly IValidateApplicationClientCredentialsUseCase applicationValidationUseCase;
    private readonly ValidateClientCredentialsUseCase useCase;

    public ValidateClientCredentialsUseCaseTests()
    {
        this.applicationValidationUseCase = Substitute.For<IValidateApplicationClientCredentialsUseCase>();
        this.useCase = new ValidateClientCredentialsUseCase(this.applicationValidationUseCase);
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesToUnifiedApplicationCredentialValidation()
    {
        var applicationId = new DomainApplicationId(Guid.NewGuid());
        this.applicationValidationUseCase
            .ExecuteAsync(
                Arg.Is<ValidateApplicationClientCredentialsCommand>(command =>
                    command.ClientId == "worker" && command.ClientSecret == "secret"),
                Arg.Any<CancellationToken>())
            .Returns(new ValidateApplicationCredentialsResult(
                applicationId,
                "worker",
                "Worker",
                ["api"],
                ["client_credentials"]));

        Result<ValidateClientCredentialsResult> result = await this.useCase.ExecuteAsync(
            new ValidateClientCredentialsCommand("worker", "secret"));

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
            .ExecuteAsync(Arg.Any<ValidateApplicationClientCredentialsCommand>(), Arg.Any<CancellationToken>())
            .Returns(DomainError.Unauthorized(applicationErrorCode, "Validation failed."));

        Result<ValidateClientCredentialsResult> result = await this.useCase.ExecuteAsync(
            new ValidateClientCredentialsCommand("worker", "secret"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(expectedLegacyErrorCode);
    }
}
