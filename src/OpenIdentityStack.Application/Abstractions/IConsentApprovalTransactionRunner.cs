namespace OpenIdentityStack.Application.Abstractions;

public interface IConsentApprovalTransactionRunner
{
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
