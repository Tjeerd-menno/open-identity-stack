using Microsoft.EntityFrameworkCore.Storage;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Infrastructure.Persistence;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.ApplicationPermissions;

public sealed class ApplicationPermissionTransactionRunner : IApplicationPermissionTransactionRunner
{
    private readonly OpenIdentityStackDbContext dbContext;

    public ApplicationPermissionTransactionRunner(OpenIdentityStackDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Result<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken = default)
    {
        await using IDbContextTransaction transaction = await this.dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            Result<T> result = await operation(cancellationToken).ConfigureAwait(false);
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
