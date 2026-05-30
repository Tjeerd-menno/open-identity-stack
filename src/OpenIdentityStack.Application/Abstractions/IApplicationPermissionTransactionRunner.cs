using SharedKernel;

namespace OpenIdentityStack.Application.Abstractions;

public interface IApplicationPermissionTransactionRunner
{
    Task<Result<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken = default);
}
