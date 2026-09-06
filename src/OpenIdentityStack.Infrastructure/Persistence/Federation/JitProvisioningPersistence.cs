using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Persistence.Federation;

/// <summary>Owns the transaction for a JIT account association and its creation audit.</summary>
public sealed class JitProvisioningPersistence(OpenIdentityStackDbContext db, IAuditLog audit) : IJitProvisioningPersistence
{
    public async Task<Result> CommitAsync(UserId userId, UpstreamProviderId providerId, bool isNewUser, CancellationToken cancellationToken = default)
    {
        try
        {
            await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            if (isNewUser)
            {
                // The conditional no-op update locks the current provider row until account creation commits.
                // Policy updates wait behind it; a policy update that already committed makes this predicate fail.
                int permitted = await db.UpstreamProviders
                    .Where(provider => provider.Id == providerId && provider.Status == ProviderStatus.Active
                        && (!isNewUser || provider.JitProvisioningEnabled))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(provider => provider.JitProvisioningEnabled,
                        provider => provider.JitProvisioningEnabled), cancellationToken);
                if (permitted != 1)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    db.ChangeTracker.Clear();
                    await audit.LogAsync("federation", "Federation.AccountAssociationDenied", "UpstreamProvider", providerId.Value.ToString(),
                        "Current provider policy denies authentication or new-account provisioning.", cancellationToken);
                    return DomainError.Forbidden("Federation.AuthenticationFailed", "Unable to complete sign-in.");
                }
            }
            await db.SaveChangesAsync(cancellationToken);
            if (isNewUser)
            {
                await audit.LogAsync("federation", "Federation.NewAccountAssociationRecorded", "User", userId.Value.ToString(),
                    "Independent new-account provisioning evidence recorded.", cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException exception) when (IsIdentityConflict(exception))
        {
            // The transaction has disposed and rolled back. Never let the denial audit flush failed tracked additions.
            db.ChangeTracker.Clear();
            await audit.LogAsync("federation", "Federation.AccountAssociationDenied", "UpstreamProvider", providerId.Value.ToString(),
                "Concurrent identity or account state changed; authentication was denied without transferring access.", cancellationToken);
            return DomainError.Forbidden("Federation.AuthenticationFailed", "Unable to complete sign-in.");
        }
        catch
        {
            // Cancellation, audit failures and unrelated database failures remain failures, with no pending writes to retry.
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private static bool IsIdentityConflict(DbUpdateException exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return exception.Entries.Count > 0 && exception.Entries.All(entry => entry.Entity is User or UpstreamProvider);
        }
        return exception.InnerException switch
        {
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName:
                "IX_Users_NormalizedEmail" or "IX_UserUpstreamIdentities_ProviderId_SubjectId" } => true,
            SqliteException { SqliteExtendedErrorCode: 2067 } sqlite =>
                sqlite.Message.Contains("UNIQUE constraint failed: Users.NormalizedEmail'", StringComparison.Ordinal)
                || sqlite.Message.Contains("UNIQUE constraint failed: UserUpstreamIdentities.ProviderId, UserUpstreamIdentities.SubjectId'", StringComparison.Ordinal),
            _ => false
        };
    }
}
