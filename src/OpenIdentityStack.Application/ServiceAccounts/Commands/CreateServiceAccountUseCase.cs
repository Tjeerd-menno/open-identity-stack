using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Use case implementation for creating service accounts.
/// </summary>
public sealed class CreateServiceAccountUseCase : ICreateServiceAccountUseCase
{
    private readonly IServiceAccountRepository repository;
    private readonly IPasswordHasher passwordHasher;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;
    private readonly IClientApplicationRegistrar clientApplicationRegistrar;

    public CreateServiceAccountUseCase(
        IServiceAccountRepository repository,
        IPasswordHasher passwordHasher,
        IDateTimeProvider dateTimeProvider,
        IAuditLog auditLog,
        IClientApplicationRegistrar? clientApplicationRegistrar = null)
    {
        this.repository = repository;
        this.passwordHasher = passwordHasher;
        this.dateTimeProvider = dateTimeProvider;
        this.auditLog = auditLog;
        this.clientApplicationRegistrar = clientApplicationRegistrar ?? new NoopClientApplicationRegistrar();
    }

    /// <inheritdoc />
    public async Task<Result<CreateServiceAccountResponse>> ExecuteAsync(
        CreateServiceAccountCommand command, 
        CancellationToken cancellationToken = default)
    {
        // Check if client_id already exists
        if (await this.repository.ExistsByClientIdAsync(command.ClientId, cancellationToken).ConfigureAwait(false))
        {
            return ServiceAccountErrors.ClientIdExists;
        }

        // Create the service account
        Result<ServiceAccount> createResult = ServiceAccount.Create(
            command.ClientId,
            command.DisplayName,
            command.AllowedScopes,
            command.AllowedGrantTypes,
            this.dateTimeProvider);

        if (createResult.IsFailure)
        {
            return createResult.Error;
        }

        ServiceAccount serviceAccount = createResult.Value;

        // Generate initial secret
        string plainSecret = GenerateSecret();
        string hashedSecret = this.passwordHasher.HashPassword(plainSecret);

        // Add the initial credential
        serviceAccount.AddCredential(
            hashedSecret,
            "Initial credential",
            expiresAt: null,
            this.dateTimeProvider);

        // Track the service account in the repository (not yet persisted).
        await this.repository.AddAsync(serviceAccount, cancellationToken);

        // Register as OAuth client BEFORE committing.
        // If registration fails, the service account is not persisted (atomicity is preserved).
        Result registrationResult = await this.clientApplicationRegistrar.RegisterServiceAccountAsync(
            serviceAccount.ClientId,
            serviceAccount.DisplayName,
            plainSecret,
            serviceAccount.AllowedScopes,
            serviceAccount.AllowedGrantTypes,
            cancellationToken);

        if (registrationResult.IsFailure)
        {
            return registrationResult.Error;
        }

        // Persist any remaining pending changes.
        // This is a no-op when the registrar already called SaveChangesAsync (OpenIddict path),
        // and performs the actual save when using the no-op registrar.
        await this.repository.SaveChangesAsync(cancellationToken);

        // Audit log
        await this.auditLog.LogAsync(
            userId: "system",
            action: "ServiceAccount.Created",
            entityType: "ServiceAccount",
                entityId: serviceAccount.Id.Value.ToString(),
                details: $"ClientId: {command.ClientId}, DisplayName: {command.DisplayName}",
                cancellationToken: cancellationToken);

            return new CreateServiceAccountResponse(
            serviceAccount.Id,
            serviceAccount.ClientId,
            serviceAccount.DisplayName,
            plainSecret);
    }

    private static string GenerateSecret()
    {
        // Generate a cryptographically secure random secret
        byte[] bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private sealed class NoopClientApplicationRegistrar : IClientApplicationRegistrar
    {
        public Task<Result> RegisterServiceAccountAsync(
            string clientId,
            string displayName,
            string clientSecret,
            IReadOnlyList<string> allowedScopes,
            IReadOnlyList<string> allowedGrantTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }
}
