using SharedKernel;

namespace OpenIdentityStack.Application.Abstractions;

public static class CredentialBoundaryClaims
{
    public const string Epoch = "ois_credential_boundary";
}

public sealed record CredentialCutoverResult(Guid OperationId, DateTimeOffset CompletedAt, long Tokens, long Grants, int Sessions);

public interface ICredentialBoundaryStore
{
    Task<Guid> GetEpochAsync(CancellationToken cancellationToken = default);
    Task<bool> IsCurrentAsync(string? epoch, CancellationToken cancellationToken = default);
    Task<CredentialCutoverResult> ExecuteAsync(Guid operationId, string actorId, CancellationToken cancellationToken = default);
}

public sealed class ExecuteCredentialCutover(ICredentialBoundaryStore store, IAdministrativeApproval approval, IAdministrativeActorContext actor)
{
    public async Task<Result<CredentialCutoverResult>> ExecuteAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        if (operationId == Guid.Empty) { return DomainError.Validation("CredentialCutover.OperationRequired", "A nonempty operation identifier is required."); }
        Result approved = await approval.RequireAsync("CredentialBoundary.Cutover", operationId.ToString(), cancellationToken: cancellationToken);
        if (approved.IsFailure) { return approved.Error; }
        CredentialCutoverResult result = await store.ExecuteAsync(operationId, actor.Current!.UserId.Value.ToString(), cancellationToken);
        await approval.RecordOutcomeAsync(true, cancellationToken);
        return result;
    }
}
