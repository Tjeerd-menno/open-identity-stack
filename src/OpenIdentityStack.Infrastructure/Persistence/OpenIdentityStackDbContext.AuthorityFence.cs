using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Infrastructure.Persistence;

public partial class OpenIdentityStackDbContext
{
    private long? authoritySnapshot;

    internal async Task CaptureAuthoritySnapshotAsync(CancellationToken cancellationToken)
    {
        if (this.authoritySnapshot.HasValue) { return; }
        // A guarded use case must capture before mutation; stale tracked authorization reads
        // cannot be reused under a newer revision.
        if (this.ChangeTracker.HasChanges()) { throw new InvalidOperationException("Capture administrative authority before modifying tracked entities."); }
        long revision = await this.Set<AdministrativeAuthorityRevision>().AsNoTracking().Select(value => value.Revision).SingleAsync(cancellationToken);
        this.ChangeTracker.Clear();
        this.authoritySnapshot = revision;
    }

    private bool NeedsAuthorityFence() => this.ChangeTracker.Entries().Any(entry =>
        entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
        (entry.Entity is Role or RoleAssignment or Group or GroupMapping or GroupMembership
            || entry.Entity is User && (entry.State != EntityState.Modified || Changed(entry, nameof(User.Status), nameof(User.PasswordHash), "CredentialRevision"))
            // These later feature slices use the same save boundary; matching model names
            // keeps the fence independent of resource-domain contracts.
            || entry.Metadata.ClrType.Name is "Application" or "ApplicationCredential" or "ClientResourceGrant" or "ProtectedResource" or "CredentialBoundaryState"));

    private static bool Changed(EntityEntry entry, params string[] fields) => fields.Any(field =>
        entry.Metadata.FindProperty(field) is not null && entry.Property(field).IsModified);

    private IQueryable<AdministrativeAuthorityRevision> ExpectedAuthority() => this.Set<AdministrativeAuthorityRevision>()
        .Where(value => value.Id == 1 && (!this.authoritySnapshot.HasValue || value.Revision == this.authoritySnapshot.Value));

    private int SaveWithAuthorityFence(bool acceptAllChangesOnSuccess)
    {
        if (!this.NeedsAuthorityFence()) { return base.SaveChanges(acceptAllChangesOnSuccess); }
        if (!this.Database.IsRelational())
        {
            this.PrepareNonRelationalFence();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        using IDbContextTransaction? owned = this.Database.CurrentTransaction is null ? this.Database.BeginTransaction() : null;
        IDbContextTransaction transaction = owned ?? this.Database.CurrentTransaction!;
        string? savepoint = owned is null ? $"authority_{Guid.NewGuid():N}" : null;
        if (savepoint is not null) { transaction.CreateSavepoint(savepoint); }
        try
        {
            if (this.ExpectedAuthority().ExecuteUpdate(update => update.SetProperty(value => value.Revision, value => value.Revision + 1)) != 1)
            { throw new AdministrativeAuthorityConcurrencyException("Administrative authority changed; repeat the operation against current authority."); }
            int result = base.SaveChanges(acceptAllChangesOnSuccess);
            if (savepoint is not null) { transaction.ReleaseSavepoint(savepoint); }
            owned?.Commit();
            this.authoritySnapshot = null;
            return result;
        }
        catch
        {
            if (owned is not null) { owned.Rollback(); }
            else if (savepoint is not null) { transaction.RollbackToSavepoint(savepoint); }
            this.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<int> SaveWithAuthorityFenceAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken)
    {
        if (!this.NeedsAuthorityFence()) { return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
        if (!this.Database.IsRelational())
        {
            this.PrepareNonRelationalFence();
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        await using IDbContextTransaction? owned = this.Database.CurrentTransaction is null ? await this.Database.BeginTransactionAsync(cancellationToken) : null;
        IDbContextTransaction transaction = owned ?? this.Database.CurrentTransaction!;
        string? savepoint = owned is null ? $"authority_{Guid.NewGuid():N}" : null;
        if (savepoint is not null) { await transaction.CreateSavepointAsync(savepoint, cancellationToken); }
        try
        {
            if (await this.ExpectedAuthority().ExecuteUpdateAsync(update => update.SetProperty(value => value.Revision, value => value.Revision + 1), cancellationToken) != 1)
            { throw new AdministrativeAuthorityConcurrencyException("Administrative authority changed; repeat the operation against current authority."); }
            int result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            if (savepoint is not null) { await transaction.ReleaseSavepointAsync(savepoint, cancellationToken); }
            if (owned is not null) { await owned.CommitAsync(cancellationToken); }
            this.authoritySnapshot = null;
            return result;
        }
        catch
        {
            if (owned is not null) { await owned.RollbackAsync(CancellationToken.None); }
            else if (savepoint is not null) { await transaction.RollbackToSavepointAsync(savepoint, CancellationToken.None); }
            this.ChangeTracker.Clear();
            throw;
        }
    }

    private void PrepareNonRelationalFence()
    {
        AdministrativeAuthorityRevision current = this.Set<AdministrativeAuthorityRevision>().Single();
        if (this.authoritySnapshot is { } expected && current.Revision != expected)
        { throw new AdministrativeAuthorityConcurrencyException("Administrative authority changed."); }
        current.Revision++;
        this.authoritySnapshot = null;
    }
}
