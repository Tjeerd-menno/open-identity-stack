using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Persistence;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Audit;

/// <summary>Records authority changes in the same transaction as their persistence.</summary>
public sealed class AdministrativeAuthorityAuditInterceptor(
    IDateTimeProvider clock, IAdministrativeActorContext actor) : SaveChangesInterceptor
{
    private readonly List<AuditLogEntry> pending = [];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        this.Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        this.Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        this.pending.Clear();
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        this.pending.Clear();
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => this.DiscardPending(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        this.DiscardPending(eventData.Context);
        return Task.CompletedTask;
    }

    private void Capture(DbContext? context)
    {
        if (context is not OpenIdentityStackDbContext db) { return; }
        this.DiscardPending(db);
        db.ChangeTracker.DetectChanges();
        foreach (EntityEntry entry in db.ChangeTracker.Entries().ToArray())
        {
            AuthorityChange? change = Describe(entry);
            if (change is null || change.Fields.Length == 0) { continue; }
            var audit = new AuditLogEntry
            {
                UserId = actor.AuditActorId,
                Action = "AdministrativeAuthorityChanged",
                EntityType = change.EntityType,
                EntityId = change.EntityId,
                Details = JsonSerializer.Serialize(new { Operation = entry.State.ToString(), change.Fields }),
                Timestamp = clock.UtcNow,
            };
            this.pending.Add(audit);
            db.AuditLogEntries.Add(audit);
        }
    }

    private static AuthorityChange? Describe(EntityEntry entry)
    {
        // Only identity keys and an explicit field-name allowlist are recorded. Never serialize an entity or property values.
        if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) { return null; }
        return entry.Entity switch
        {
            Role role when entry.State != EntityState.Added => new(nameof(Role), role.Id.Value.ToString(), ChangedFields(entry, nameof(Role.Permissions), nameof(Role.IsActive))),
            RoleAssignment assignment when entry.State != EntityState.Modified => new(nameof(RoleAssignment), $"{assignment.UserId.Value}:{assignment.RoleId.Value}", [nameof(RoleAssignment.UserId), nameof(RoleAssignment.RoleId)]),
            GroupMembership membership when entry.State != EntityState.Modified => new(nameof(GroupMembership), $"{membership.GroupId.Value}:{membership.UserId.Value}", [nameof(GroupMembership.GroupId), nameof(GroupMembership.UserId)]),
            GroupMapping mapping when mapping.Type == MappingType.Role && entry.State != EntityState.Modified => new(nameof(GroupMapping), entry.Property("Id").CurrentValue!.ToString()!, [nameof(GroupMapping.Type), nameof(GroupMapping.Target)]),
            Group group when entry.State == EntityState.Deleted => new(nameof(Group), group.Id.Value.ToString(), [nameof(Group.Memberships), nameof(Group.Mappings)]),
            User user when entry.State != EntityState.Added => new(nameof(User), user.Id.Value.ToString(), ChangedFields(entry, nameof(User.Status))),
            Domain.Applications.Application application when entry.State != EntityState.Added => new(nameof(Domain.Applications.Application), application.Id.Value.ToString(),
                ChangedFields(entry, nameof(Domain.Applications.Application.Status), "allowedScopes")),
            _ => null,
        };
    }

    private static string[] ChangedFields(EntityEntry entry, params string[] fields) =>
        entry.State == EntityState.Deleted ? fields : fields.Where(field => IsChanged(entry.Property(field))).ToArray();

    private static bool IsChanged(PropertyEntry property)
    {
        if (!property.IsModified) { return false; }
        return property.OriginalValue is IEnumerable<string> before && property.CurrentValue is IEnumerable<string> after
            ? !before.SequenceEqual(after, StringComparer.Ordinal)
            : !Equals(property.OriginalValue, property.CurrentValue);
    }

    private sealed record AuthorityChange(string EntityType, string EntityId, string[] Fields);

    private void DiscardPending(DbContext? context)
    {
        if (context is not null)
        {
            foreach (AuditLogEntry audit in this.pending)
            {
                EntityEntry<AuditLogEntry> entry = context.Entry(audit);
                if (entry.State == EntityState.Added) { entry.State = EntityState.Detached; }
            }
        }
        this.pending.Clear();
    }
}
