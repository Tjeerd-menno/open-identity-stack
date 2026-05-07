using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Use case implementation for updating service accounts.
/// </summary>
public sealed class UpdateServiceAccountUseCase : IUpdateServiceAccountUseCase
{
    private readonly IServiceAccountRepository repository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;

    public UpdateServiceAccountUseCase(
        IServiceAccountRepository repository,
        IDateTimeProvider dateTimeProvider,
        IAuditLog auditLog)
    {
        this.repository = repository;
        this.dateTimeProvider = dateTimeProvider;
        this.auditLog = auditLog;
    }

    /// <inheritdoc />
    public async Task<Result<UpdateServiceAccountResult>> ExecuteAsync(
        UpdateServiceAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        ServiceAccount? serviceAccount = await this.repository.GetByIdAsync(command.ServiceAccountId, cancellationToken);
        if (serviceAccount is null)
        {
            return ServiceAccountErrors.NotFound;
        }

        if (!string.IsNullOrWhiteSpace(command.DisplayName))
        {
            Result updateResult = serviceAccount.UpdateDisplayName(command.DisplayName, this.dateTimeProvider);
            if (updateResult.IsFailure)
            {
                return updateResult.Error;
            }
        }

        await this.repository.SaveChangesAsync(cancellationToken);

        await this.auditLog.LogAsync(
            userId: "system",
            action: "ServiceAccount.Updated",
            entityType: "ServiceAccount",
            entityId: serviceAccount.Id.Value.ToString(),
            details: $"DisplayName: {command.DisplayName}",
            cancellationToken: cancellationToken);

        return new UpdateServiceAccountResult(command.ServiceAccountId, this.dateTimeProvider.UtcNow);
    }
}
