using Microsoft.EntityFrameworkCore.Storage;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Infrastructure.Persistence;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Identity;

public sealed class CredentialLifecycleTransactionRunner : ICredentialLifecycleTransactionRunner
{
    private readonly OpenIdentityStackDbContext dbContext;

    public CredentialLifecycleTransactionRunner(OpenIdentityStackDbContext dbContext) => this.dbContext = dbContext;

    public async Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken cancellationToken = default)
    {
        await using IDbContextTransaction transaction = await this.dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            Result<T> result = await operation(cancellationToken);
            if (result.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return result;
            }
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
