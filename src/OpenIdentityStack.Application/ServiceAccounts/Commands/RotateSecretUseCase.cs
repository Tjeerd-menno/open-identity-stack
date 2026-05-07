using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Use case implementation for rotating service account secrets.
/// </summary>
public sealed class RotateSecretUseCase : IRotateSecretUseCase
{
    private readonly IServiceAccountRepository repository;
    private readonly IPasswordHasher passwordHasher;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;

    public RotateSecretUseCase(
        IServiceAccountRepository repository,
        IPasswordHasher passwordHasher,
        IDateTimeProvider dateTimeProvider,
        IAuditLog auditLog)
    {
        this.repository = repository;
        this.passwordHasher = passwordHasher;
        this.dateTimeProvider = dateTimeProvider;
        this.auditLog = auditLog;
    }

    /// <inheritdoc />
    public async Task<Result<RotateSecretResponse>> ExecuteAsync(
        RotateSecretCommand command, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteInternalAsync(command, cancellationToken);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            // Retry once in case a concurrent OpenIddict update conflicted with this save.
            return await this.ExecuteInternalAsync(command, cancellationToken);
        }
    }

    private async Task<Result<RotateSecretResponse>> ExecuteInternalAsync(
        RotateSecretCommand command,
        CancellationToken cancellationToken)
    {
        // Get the service account
        ServiceAccount? serviceAccount = await this.repository.GetByIdAsync(command.ServiceAccountId, cancellationToken);
        if (serviceAccount is null)
        {
            return ServiceAccountErrors.NotFound;
        }

        // Check if disabled
        if (serviceAccount.Status == ServiceAccountStatus.Disabled)
        {
            return ServiceAccountErrors.Disabled;
        }

        // Revoke existing credentials if requested
        if (command.RevokeExisting)
        {
            foreach (ClientCredential? credential in serviceAccount.Credentials.Where(c => !c.IsRevoked))
            {
                serviceAccount.RevokeCredential(credential.Id, this.dateTimeProvider);
            }
        }

        // Generate new secret
        string plainSecret = GenerateSecret();
        string hashedSecret = this.passwordHasher.HashPassword(plainSecret);

        // Add new credential
        Result<Guid> addResult = serviceAccount.AddCredential(
            hashedSecret,
            command.Description,
            command.ExpiresAt,
            this.dateTimeProvider);

        if (addResult.IsFailure)
        {
            return addResult.Error;
        }

        // Persist
        await this.repository.SaveChangesAsync(cancellationToken);

        // Audit log
        await this.auditLog.LogAsync(
            userId: "system",
            action: "ServiceAccount.SecretRotated",
            entityType: "ServiceAccount",
            entityId: serviceAccount.Id.Value.ToString(),
            details: $"CredentialId: {addResult.Value}, RevokeExisting: {command.RevokeExisting}",
            cancellationToken: cancellationToken);

        return new RotateSecretResponse(addResult.Value, plainSecret);
    }

    private static string GenerateSecret()
    {
        byte[] bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
