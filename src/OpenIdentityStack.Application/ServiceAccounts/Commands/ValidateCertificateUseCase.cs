using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Use case for validating client certificate authentication.
/// </summary>
public sealed class ValidateCertificateUseCase : IValidateCertificateUseCase
{
    private readonly IValidateApplicationCertificateUseCase applicationValidationUseCase;

    public ValidateCertificateUseCase(IValidateApplicationCertificateUseCase applicationValidationUseCase)
    {
        this.applicationValidationUseCase = applicationValidationUseCase;
    }

    public async Task<Result<ValidateCertificateResult>> ExecuteAsync(
        ValidateCertificateCommand command,
        CancellationToken cancellationToken = default)
    {
        Result<ValidateApplicationCredentialsResult> result =
            await this.applicationValidationUseCase.ExecuteAsync(
                new ValidateApplicationCertificateCommand(command.ClientId, command.CertificateThumbprint),
                cancellationToken);

        if (result.IsFailure)
        {
            return MapError(result.Error);
        }

        ValidateApplicationCredentialsResult value = result.Value;
        return new ValidateCertificateResult(
            new ServiceAccountId(value.ApplicationId.Value),
            value.ClientId,
            value.DisplayName,
            value.AllowedScopes,
            value.AllowedGrantTypes);
    }

    private static DomainError MapError(DomainError error)
    {
        if (error.Code.EndsWith("Application.ClientIdRequired", StringComparison.Ordinal))
        {
            return ServiceAccountErrors.ClientIdRequired;
        }

        if (error.Code.EndsWith("Application.Disabled", StringComparison.Ordinal))
        {
            return ServiceAccountErrors.AccountDisabled;
        }

        if (error.Code.EndsWith("Application.InvalidCredentials", StringComparison.Ordinal))
        {
            return ServiceAccountErrors.InvalidCredentials;
        }

        return error;
    }
}
