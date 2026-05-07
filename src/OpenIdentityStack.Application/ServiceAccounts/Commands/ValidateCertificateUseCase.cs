using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Use case for validating client certificate authentication.
/// </summary>
public sealed class ValidateCertificateUseCase : IValidateCertificateUseCase
{
    private readonly IServiceAccountRepository serviceAccountRepository;
    private readonly IDateTimeProvider dateTimeProvider;

    public ValidateCertificateUseCase(
        IServiceAccountRepository serviceAccountRepository,
        IDateTimeProvider dateTimeProvider)
    {
        this.serviceAccountRepository = serviceAccountRepository;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<ValidateCertificateResult>> ExecuteAsync(
        ValidateCertificateCommand command,
        CancellationToken cancellationToken = default)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(command.ClientId))
        {
            return ServiceAccountErrors.ClientIdRequired;
        }

        if (string.IsNullOrWhiteSpace(command.CertificateThumbprint))
        {
            return ServiceAccountErrors.InvalidCredentials;
        }

        // Find the service account
        ServiceAccount? serviceAccount = await this.serviceAccountRepository.GetByClientIdAsync(
            command.ClientId,
            cancellationToken);

        if (serviceAccount is null)
        {
            return ServiceAccountErrors.InvalidCredentials;
        }

        // Check if the account is active
        if (serviceAccount.Status != ServiceAccountStatus.Active)
        {
            return ServiceAccountErrors.AccountDisabled;
        }

        // Find a valid certificate that matches the thumbprint
        ClientCertificate? matchingCertificate = serviceAccount.FindValidCertificate(
            command.CertificateThumbprint,
            this.dateTimeProvider);

        if (matchingCertificate is null)
        {
            return ServiceAccountErrors.InvalidCredentials;
        }

        return new ValidateCertificateResult(
            serviceAccount.Id,
            serviceAccount.ClientId,
            serviceAccount.DisplayName,
            serviceAccount.AllowedScopes,
            serviceAccount.AllowedGrantTypes);
    }
}
