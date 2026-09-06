using Microsoft.EntityFrameworkCore.Storage;
using OpenIdentityStack.Infrastructure.Persistence;

namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>
/// Keeps issuance writes invisible until subject metadata and final credential checks are complete.
/// </summary>
public sealed class TokenIssuanceTransaction(OpenIdentityStackDbContext dbContext) : IAsyncDisposable
{
    private IDbContextTransaction? transaction;

    public async Task BeginAsync(CancellationToken cancellationToken)
    {
        if (this.transaction is not null || dbContext.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("A token issuance transaction is already active.");
        }

        this.transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.LockCredentialBoundaryAsync(cancellationToken);
        }
        catch
        {
            await this.RollbackAndDisposeAsync();
            throw;
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        IDbContextTransaction current = this.transaction
            ?? throw new InvalidOperationException("No token issuance transaction is active.");
        try
        {
            await current.CommitAsync(cancellationToken);
        }
        finally
        {
            await current.DisposeAsync();
            this.transaction = null;
        }
    }

    public ValueTask DisposeAsync() => this.transaction is null
        ? ValueTask.CompletedTask
        : new ValueTask(this.RollbackAndDisposeAsync());

    private async Task RollbackAndDisposeAsync()
    {
        IDbContextTransaction? current = this.transaction;
        this.transaction = null;
        if (current is null) { return; }
        try
        {
            await current.RollbackAsync(CancellationToken.None);
        }
        finally
        {
            await current.DisposeAsync();
        }
    }
}
