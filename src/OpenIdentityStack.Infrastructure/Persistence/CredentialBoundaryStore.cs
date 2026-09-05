using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIddict.Abstractions;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Infrastructure.Audit;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Persistence;

public sealed class CredentialBoundaryState
{
    public int Id { get; set; } = 1;
    public Guid Epoch { get; set; }
}

public sealed class CredentialCutoverRecord
{
    public Guid Id { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public long Tokens { get; set; }
    public long Grants { get; set; }
    public int Sessions { get; set; }
    public CredentialCutoverResult ToResult() => new(this.Id, this.CompletedAt, this.Tokens, this.Grants, this.Sessions);
}

public sealed class CredentialBoundaryConfiguration : IEntityTypeConfiguration<CredentialBoundaryState>
{
    public void Configure(EntityTypeBuilder<CredentialBoundaryState> builder)
    {
        builder.ToTable("CredentialBoundary");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Epoch).IsConcurrencyToken();
        builder.HasData(new CredentialBoundaryState());
    }
}

public sealed class CredentialCutoverConfiguration : IEntityTypeConfiguration<CredentialCutoverRecord>
{
    public void Configure(EntityTypeBuilder<CredentialCutoverRecord> builder)
    {
        builder.ToTable("CredentialCutovers");
        builder.HasKey(x => x.Id);
    }
}

public sealed class CredentialBoundaryStore(OpenIdentityStackDbContext db, IOpenIddictTokenManager tokens,
    IOpenIddictAuthorizationManager grants, IDateTimeProvider clock, OpenIdentityStack.Application.Abstractions.ICredentialCutoverGate gate) : ICredentialBoundaryStore
{
    // Fresh scalar reads deliberately bypass EF's identity map and all process-local caches.
    public Task<Guid> GetEpochAsync(CancellationToken cancellationToken = default) =>
        db.Set<CredentialBoundaryState>().Where(x => x.Id == 1).Select(x => x.Epoch).SingleAsync(cancellationToken);

    public async Task<bool> IsCurrentAsync(string? epoch, CancellationToken cancellationToken = default)
    {
        Guid current = await this.GetEpochAsync(cancellationToken);
        return epoch is null ? current == Guid.Empty : Guid.TryParse(epoch, out Guid captured) && current == captured;
    }

    public async Task<Result<CredentialCutoverResult>> ExecuteAsync(Guid operationId, string actorId, CancellationToken cancellationToken = default)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        CredentialCutoverRecord? previous = await db.Set<CredentialCutoverRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == operationId, cancellationToken);
        if (previous is not null) { return previous.ToResult(); }
        CredentialCutoverPreflight preflight = await gate.EvaluateAsync(cancellationToken);
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            UserId = actorId, Action = preflight.Ready ? "CredentialCutover.PreflightPassed" : "CredentialCutover.PreflightBlocked",
            EntityType = "CredentialBoundary", EntityId = operationId.ToString(), Timestamp = clock.UtcNow,
            Details = "Cutover prerequisites rechecked inside the serializable transaction.",
            AfterState = System.Text.Json.JsonSerializer.Serialize(preflight)
        });
        if (!preflight.Ready)
        {
            // Commit only the blocked diagnostic; the boundary, grants, and sessions are unchanged.
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return DomainError.Conflict("CredentialCutover.PrerequisitesUnresolved", "Cutover is blocked. Review the current migration preflight and resolve every prerequisite.");
        }
        CredentialBoundaryState boundary = await db.Set<CredentialBoundaryState>().SingleAsync(x => x.Id == 1, cancellationToken);
        boundary.Epoch = operationId;
        // Save the concurrency-checked boundary before revocation. All changes commit together.
        await db.SaveChangesAsync(cancellationToken);
        long revokedTokens = await tokens.RevokeAsync(null, null, null, null, cancellationToken);
        long revokedGrants = await grants.RevokeAsync(null, null, null, null, cancellationToken);
        DateTimeOffset revokedAt = clock.UtcNow;
        int revokedSessions = await db.UserSessions
            .Where(x => x.Status == SessionStatus.Active)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, SessionStatus.Revoked)
                .SetProperty(x => x.RevokedAt, revokedAt), cancellationToken);
        var record = new CredentialCutoverRecord { Id = operationId, CompletedAt = revokedAt, Tokens = revokedTokens, Grants = revokedGrants, Sessions = revokedSessions };
        db.Set<CredentialCutoverRecord>().Add(record);
        db.AuditLogEntries.Add(new AuditLogEntry { UserId = actorId, Action = "CredentialBoundary.CutoverCompleted", EntityType = "CredentialBoundary", EntityId = operationId.ToString(), Timestamp = record.CompletedAt,
            Details = $"Revoked {revokedTokens} tokens, {revokedGrants} grants and {revokedSessions} sessions. Offline resource validators require separate action." });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return record.ToResult();
    }
}
