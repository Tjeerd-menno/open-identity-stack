using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Use case implementation for enabling service accounts.
/// </summary>
public sealed class EnableServiceAccountUseCase : IEnableServiceAccountUseCase
{
    private readonly IServiceAccountRepository repository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;

    public EnableServiceAccountUseCase(
        IServiceAccountRepository repository,
        IDateTimeProvider dateTimeProvider,
        IAuditLog auditLog)
    {
        this.repository = repository;
        this.dateTimeProvider = dateTimeProvider;
        this.auditLog = auditLog;
    }

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(
            EnableServiceAccountCommand command,
            CancellationToken cancellationToken = default)
        {
            ServiceAccount? serviceAccount = await this.repository.GetByIdAsync(command.ServiceAccountId, cancellationToken);
            if (serviceAccount is null)
        {
            return ServiceAccountErrors.NotFound;
        }

        Result enableResult = serviceAccount.Enable(this.dateTimeProvider);
        if (enableResult.IsFailure)
        {
            return enableResult.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken);

        await this.auditLog.LogAsync(
            userId: "system",
            action: "ServiceAccount.Enabled",
            entityType: "ServiceAccount",
            entityId: serviceAccount.Id.Value.ToString(),
            details: $"ClientId: {serviceAccount.ClientId}",
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
