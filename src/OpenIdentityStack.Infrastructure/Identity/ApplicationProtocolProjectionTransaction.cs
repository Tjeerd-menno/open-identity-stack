using Microsoft.EntityFrameworkCore.Storage;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Infrastructure.Persistence;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Identity;

public sealed class ApplicationProtocolProjectionTransaction(OpenIdentityStackDbContext dbContext)
    : IApplicationProtocolProjectionTransaction
{
    public async Task<Result> ExecuteAsync(
        Func<CancellationToken, Task<Result>> operation,
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }

        await using IDbContextTransaction transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            Result result = await operation(cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return result;
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}
