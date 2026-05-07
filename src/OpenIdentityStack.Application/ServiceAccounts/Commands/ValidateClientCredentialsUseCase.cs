using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Use case for validating client credentials (client_id + client_secret).
/// </summary>
public sealed class ValidateClientCredentialsUseCase : IValidateClientCredentialsUseCase
{
    private readonly IServiceAccountRepository serviceAccountRepository;
    private readonly IPasswordHasher passwordHasher;
    private readonly IDateTimeProvider dateTimeProvider;

    public ValidateClientCredentialsUseCase(
        IServiceAccountRepository serviceAccountRepository,
        IPasswordHasher passwordHasher,
        IDateTimeProvider dateTimeProvider)
    {
        this.serviceAccountRepository = serviceAccountRepository;
        this.passwordHasher = passwordHasher;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<ValidateClientCredentialsResult>> ExecuteAsync(
        ValidateClientCredentialsCommand command,
        CancellationToken cancellationToken = default)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(command.ClientId))
        {
            return ServiceAccountErrors.ClientIdRequired;
        }

        if (string.IsNullOrWhiteSpace(command.ClientSecret))
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

        // Find a valid credential that matches the secret
        ClientCredential? matchingCredential = serviceAccount.FindValidCredential(
            secretHash => this.passwordHasher.VerifyPassword(secretHash, command.ClientSecret),
            this.dateTimeProvider);

        if (matchingCredential is null)
        {
            return ServiceAccountErrors.InvalidCredentials;
        }

        // Record the credential usage
        serviceAccount.RecordCredentialUsage(matchingCredential.Id, this.dateTimeProvider);
        await this.serviceAccountRepository.SaveChangesAsync(cancellationToken);

        return new ValidateClientCredentialsResult(
            serviceAccount.Id,
            serviceAccount.ClientId,
            serviceAccount.DisplayName,
            serviceAccount.AllowedScopes,
            serviceAccount.AllowedGrantTypes);
    }
}
