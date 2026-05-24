using SharedKernel;

namespace OpenIdentityStack.Application.Applications.Commands;

public sealed record ValidateApplicationCertificateCommand(
    string ClientId,
    string CertificateThumbprint);

public interface IValidateApplicationCertificateUseCase
{
    Task<Result<ValidateApplicationCredentialsResult>> ExecuteAsync(
        ValidateApplicationCertificateCommand command,
        CancellationToken cancellationToken = default);
}

