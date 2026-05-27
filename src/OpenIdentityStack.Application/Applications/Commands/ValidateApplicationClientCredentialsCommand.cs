using SharedKernel;

namespace OpenIdentityStack.Application.Applications.Commands;

public sealed record ValidateApplicationClientCredentialsCommand(
    string ClientId,
    string ClientSecret);

public interface IValidateApplicationClientCredentialsUseCase
{
    Task<Result<ValidateApplicationCredentialsResult>> ExecuteAsync(
        ValidateApplicationClientCredentialsCommand command,
        CancellationToken cancellationToken = default);
}

