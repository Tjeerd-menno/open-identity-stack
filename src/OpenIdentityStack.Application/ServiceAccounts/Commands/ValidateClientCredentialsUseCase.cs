using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Use case for validating client credentials (client_id + client_secret).
/// </summary>
public sealed class ValidateClientCredentialsUseCase : IValidateClientCredentialsUseCase
{
    private readonly IValidateApplicationClientCredentialsUseCase applicationValidationUseCase;

    public ValidateClientCredentialsUseCase(IValidateApplicationClientCredentialsUseCase applicationValidationUseCase)
    {
        this.applicationValidationUseCase = applicationValidationUseCase;
    }

    public async Task<Result<ValidateClientCredentialsResult>> ExecuteAsync(
        ValidateClientCredentialsCommand command,
        CancellationToken cancellationToken = default)
    {
        Result<ValidateApplicationCredentialsResult> result =
            await this.applicationValidationUseCase.ExecuteAsync(
                new ValidateApplicationClientCredentialsCommand(command.ClientId, command.ClientSecret),
                cancellationToken);

        if (result.IsFailure)
        {
            return MapError(result.Error);
        }

        ValidateApplicationCredentialsResult value = result.Value;
        return new ValidateClientCredentialsResult(
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
