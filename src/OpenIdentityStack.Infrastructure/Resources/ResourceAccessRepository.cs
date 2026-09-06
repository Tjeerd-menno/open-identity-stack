using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage;
using OpenIddict.Abstractions;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Infrastructure.Persistence;
using SharedKernel;
using ApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Infrastructure.Resources;

public sealed class ResourceAccessRepository(OpenIdentityStackDbContext db, IOpenIddictScopeManager scopeManager, IDateTimeProvider clock) : IResourceAccessRepository
{
    public async Task<IReadOnlyList<ProtectedResource>> ListResourcesAsync(CancellationToken cancellationToken = default) =>
        await db.ProtectedResources.OrderBy(resource => resource.DisplayName).ToListAsync(cancellationToken);
    public Task<ProtectedResource?> GetResourceAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.ProtectedResources.SingleOrDefaultAsync(resource => resource.Id == id, cancellationToken);
    public Task<ProtectedResource?> FindByScopeAsync(string scope, CancellationToken cancellationToken = default) =>
        db.ProtectedResources.SingleOrDefaultAsync(resource => resource.Scope == scope, cancellationToken);
    public Task<ProtectedResource?> FindByAudienceAsync(string audience, CancellationToken cancellationToken = default) =>
        db.ProtectedResources.SingleOrDefaultAsync(resource => resource.Audience == audience, cancellationToken);
    public async Task<IReadOnlyList<ClientResourceGrant>> ListGrantsAsync(ApplicationId applicationId, CancellationToken cancellationToken = default) =>
        await db.ClientResourceGrants.Where(grant => grant.ClientApplicationId == applicationId).ToListAsync(cancellationToken);
    public Task<ClientResourceGrant?> GetGrantAsync(ApplicationId applicationId, Guid resourceId, CancellationToken cancellationToken = default) =>
        db.ClientResourceGrants.SingleOrDefaultAsync(grant => grant.ClientApplicationId == applicationId && grant.ResourceId == resourceId, cancellationToken);
    public void AddResource(ProtectedResource resource) => db.ProtectedResources.Add(resource);
    public void AddGrant(ClientResourceGrant grant) => db.ClientResourceGrants.Add(grant);
    public void RemoveGrant(ClientResourceGrant grant) => db.ClientResourceGrants.Remove(grant);

    public async Task SaveChangesAsync(string actorId, string action, string entityId, ProtectedResource? projectResource = null, CancellationToken cancellationToken = default)
    {
        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry in db.ChangeTracker.Entries()
            .Where(static entry => entry.Entity is ProtectedResource or ClientResourceGrant))
        {
            if (entry.State == EntityState.Added) { entry.Property("CreatedAt").CurrentValue = clock.UtcNow; }
            if (entry.State == EntityState.Modified) { entry.Property("ModifiedAt").CurrentValue = clock.UtcNow; }
        }
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            UserId = actorId, Action = action, EntityType = "ResourceAccess", EntityId = entityId, Timestamp = clock.UtcNow,
            AfterState = JsonSerializer.Serialize(new
            {
                Resources = db.ChangeTracker.Entries<ProtectedResource>().Where(static entry => entry.State is EntityState.Added or EntityState.Modified)
                    .Select(static entry => new { entry.Entity.Id, entry.Entity.Audience, entry.Entity.Scope, entry.Entity.PermissionNamespaces, entry.Entity.Enabled, entry.Entity.Revision }).ToArray(),
                Grants = db.ChangeTracker.Entries<ClientResourceGrant>().Where(static entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                    .Select(static entry => new { entry.Entity.Id, entry.Entity.ClientApplicationId, entry.Entity.ResourceId, entry.Entity.DelegatedPermissions, entry.Entity.ApplicationPermissions, entry.Entity.Revision }).ToArray()
            })
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new ResourceAccessConflictException(exception);
        }
        if (projectResource is not null)
        {
            object? existing = await scopeManager.FindByNameAsync(projectResource.Scope, cancellationToken);
            var descriptor = new OpenIddictScopeDescriptor();
            if (existing is not null) { await scopeManager.PopulateAsync(descriptor, existing, cancellationToken); }
            descriptor.Name = projectResource.Scope;
            descriptor.DisplayName = projectResource.DisplayName;
            descriptor.Resources.Clear();
            if (projectResource.Enabled) { descriptor.Resources.Add(projectResource.Audience); }
            if (existing is null) { await scopeManager.CreateAsync(descriptor, cancellationToken); }
            else { await scopeManager.UpdateAsync(existing, descriptor, cancellationToken); }
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
