using OpenIdentityStack.Application.Abstractions;
using SharedKernel;

namespace OpenIdentityStack.Application.Security.Commands;

public interface IExecuteCredentialCutoverUseCase
{
    Task<Result<CredentialCutoverResult>> ExecuteAsync(Guid operationId, CancellationToken cancellationToken = default);
}

public sealed class ExecuteCredentialCutoverUseCase(ICredentialBoundaryStore store, IAdministrativeApproval approval, IAdministrativeActorContext actor) : IExecuteCredentialCutoverUseCase
{
    public async Task<Result<CredentialCutoverResult>> ExecuteAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        if (operationId == Guid.Empty) { return DomainError.Validation("CredentialCutover.OperationRequired", "A nonempty operation identifier is required."); }
        await approval.CaptureAuthorityAsync(cancellationToken);
        Result approved = await approval.RequireAsync("CredentialBoundary.Cutover", operationId.ToString(), cancellationToken: cancellationToken);
        if (approved.IsFailure) { return approved.Error; }
        CredentialCutoverResult result = await store.ExecuteAsync(operationId, actor.Current!.UserId.Value.ToString(), cancellationToken);
        await approval.RecordOutcomeAsync(true, cancellationToken);
        return result;
    }
}
