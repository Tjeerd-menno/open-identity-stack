using SharedKernel;

namespace OpenIdentityStack.Application.Abstractions;

/// <summary>Runs credential mutations and their audit records atomically.</summary>
public interface ICredentialLifecycleTransactionRunner
{
    Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken cancellationToken = default);
}
