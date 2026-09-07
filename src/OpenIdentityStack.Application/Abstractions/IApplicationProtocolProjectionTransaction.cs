using SharedKernel;

namespace OpenIdentityStack.Application.Abstractions;

public interface IApplicationProtocolProjectionTransaction
{
    Task<Result> ExecuteAsync(
        Func<CancellationToken, Task<Result>> operation,
        CancellationToken cancellationToken = default);
}
