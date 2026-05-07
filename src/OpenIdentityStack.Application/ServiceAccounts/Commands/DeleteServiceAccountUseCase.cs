using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.ServiceAccounts;

namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Use case implementation for deleting service accounts.
/// </summary>
public sealed class DeleteServiceAccountUseCase : IDeleteServiceAccountUseCase
{
    private readonly IServiceAccountRepository repository;
    private readonly IAuditLog auditLog;

    public DeleteServiceAccountUseCase(
        IServiceAccountRepository repository,
        IAuditLog auditLog)
    {
        this.repository = repository;
        this.auditLog = auditLog;
    }

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(
        DeleteServiceAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        ServiceAccount? serviceAccount = await this.repository.GetByIdAsync(command.ServiceAccountId, cancellationToken);
        if (serviceAccount is null)
        {
            return ServiceAccountErrors.NotFound;
        }

        this.repository.Remove(serviceAccount);
        await this.repository.SaveChangesAsync(cancellationToken);

        await this.auditLog.LogAsync(
            userId: "system",
            action: "ServiceAccount.Deleted",
            entityType: "ServiceAccount",
            entityId: serviceAccount.Id.Value.ToString(),
            details: $"ClientId: {serviceAccount.ClientId}",
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
