using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
            bool recordsTrust = db.ChangeTracker.Entries<EmailVerificationEvidence>().Any(entry =>
                entry.State == EntityState.Added && entry.Entity.ProviderId == providerId.Value);
            Guid expectedTrust = recordsTrust
                ? db.ChangeTracker.Entries<UpstreamProvider>().Single(entry => entry.Entity.Id == providerId).Entity.EmailTrustVersion
                : Guid.Empty;
            // Every authentication commit locks and revalidates the provider row. Policy updates wait behind it;
            // a policy update that already committed makes the relevant predicate fail.
            int permitted = await db.UpstreamProviders
                .Where(provider => provider.Id == providerId && provider.Status == ProviderStatus.Active
                    && (!isNewUser || provider.JitProvisioningEnabled)
                    && (!recordsTrust || provider.TrustEmailVerification && provider.EmailTrustVersion == expectedTrust))
                .ExecuteUpdateAsync(setters => setters.SetProperty(provider => provider.JitProvisioningEnabled,
                    provider => provider.JitProvisioningEnabled), cancellationToken);
            if (permitted != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                await audit.LogAsync("federation", "Federation.AccountAssociationDenied", "UpstreamProvider", providerId.Value.ToString(),
                    "Current provider policy denies authentication, new-account provisioning, or new email evidence.", cancellationToken);
                return DomainError.Forbidden("Federation.AuthenticationFailed", "Unable to complete sign-in.");
            }
            if (!isNewUser && recordsTrust)
            {
                await ReconcileCommittedEvidenceAsync(userId, providerId, cancellationToken);
            }
            bool recordsNewEvidence = db.ChangeTracker.Entries<EmailVerificationEvidence>().Any(entry =>
                entry.State == EntityState.Added && entry.Entity.ProviderId == providerId.Value
                && entry.Property<UserId>("UserId").CurrentValue == userId);
            await db.SaveChangesAsync(cancellationToken);
            if (recordsNewEvidence)
            {
                await audit.LogAsync("federation", "Federation.EmailVerificationEvidenceRecorded", "User", userId.Value.ToString(),
                    $"Trusted verified-email evidence recorded from provider {providerId.Value}.", cancellationToken);
            }
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

    private async Task ReconcileCommittedEvidenceAsync(UserId userId, UpstreamProviderId providerId, CancellationToken cancellationToken)
    {
        EntityEntry<EmailVerificationEvidence>[] pending = db.ChangeTracker.Entries<EmailVerificationEvidence>()
            .Where(entry => entry.State == EntityState.Added && entry.Entity.ProviderId == providerId.Value
                && entry.Property<UserId>("UserId").CurrentValue == userId).ToArray();
        bool reconciled = false;
        foreach (EntityEntry<EmailVerificationEvidence> entry in pending)
        {
            EmailVerificationEvidence proof = entry.Entity;
            EmailVerificationEvidence? committed = await db.Users.AsNoTracking().Where(user => user.Id == userId)
                .SelectMany(user => user.EmailVerificationEvidence)
                .Where(evidence => evidence.ProviderId == providerId.Value && evidence.Issuer == proof.Issuer
                    && evidence.NormalizedEmail == proof.NormalizedEmail && evidence.WithdrawnAt == null)
                .OrderBy(evidence => evidence.VerifiedAt).ThenBy(evidence => evidence.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (committed is null) { continue; }
            // Keep the aggregate's existing object, but adopt the committed provenance rather than inserting it twice.
            // Other tracked mutations, including issuer binding and user/provider edits, remain pending.
            entry.CurrentValues.SetValues(committed);
            entry.State = EntityState.Unchanged;
            reconciled = true;
        }
        if (!reconciled) { return; }

        EntityEntry<UpstreamProvider> providerEntry = db.ChangeTracker.Entries<UpstreamProvider>().Single(entry => entry.Entity.Id == providerId);
        PropertyValues? current = await providerEntry.GetDatabaseValuesAsync(cancellationToken);
        if (current is not null && current.GetValue<string>(nameof(UpstreamProvider.Authority)) == providerEntry.Entity.Authority
            && current.GetValue<string?>(nameof(UpstreamProvider.BoundIssuer)) == providerEntry.Entity.BoundIssuer
            && current.GetValue<bool>(nameof(UpstreamProvider.IdentityConfigurationLocked)) == providerEntry.Entity.IdentityConfigurationLocked)
        {
            // A competing callback may have committed the exact same first-binding intent along with the proof.
            Guid identityVersion = current.GetValue<Guid>(nameof(UpstreamProvider.IdentityVersion));
            providerEntry.Property(provider => provider.IdentityVersion).OriginalValue = identityVersion;
            providerEntry.Property(provider => provider.IdentityVersion).CurrentValue = identityVersion;
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
