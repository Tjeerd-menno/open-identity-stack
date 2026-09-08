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
    Task<Result<CredentialCutoverResult>> ExecuteAsync(Guid operationId, string actorId, CancellationToken cancellationToken = default);
}
