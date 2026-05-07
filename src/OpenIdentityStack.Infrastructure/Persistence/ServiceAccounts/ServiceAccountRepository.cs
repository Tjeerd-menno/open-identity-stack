using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServiceAccounts;

namespace OpenIdentityStack.Infrastructure.Persistence.ServiceAccounts;

/// <summary>
/// EF Core implementation of IServiceAccountRepository.
/// </summary>
public sealed class ServiceAccountRepository : IServiceAccountRepository
{
    private readonly OpenIdentityStackDbContext dbContext;

    public ServiceAccountRepository(OpenIdentityStackDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<ServiceAccount?> GetByIdAsync(
        ServiceAccountId id,
        CancellationToken cancellationToken = default)
    {
        return await this.dbContext.ServiceAccounts
            .Include(s => s.Credentials)
            .Include(s => s.Certificates)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<ServiceAccount?> GetByClientIdAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        return await this.dbContext.ServiceAccounts
            .Include(s => s.Credentials)
            .Include(s => s.Certificates)
            .FirstOrDefaultAsync(s => s.ClientId == clientId, cancellationToken);
    }

    public async Task<bool> ExistsByClientIdAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        return await this.dbContext.ServiceAccounts
            .AnyAsync(s => s.ClientId == clientId, cancellationToken);
    }

    public async Task AddAsync(
        ServiceAccount serviceAccount,
        CancellationToken cancellationToken = default)
    {
        await this.dbContext.ServiceAccounts.AddAsync(serviceAccount, cancellationToken);
    }

    public async Task<(List<ServiceAccount> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        ServiceAccountStatus? status,
        string? searchTerm,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ServiceAccount> query = this.dbContext.ServiceAccounts
            .Include(s => s.Credentials)
            .Include(s => s.Certificates)
            .AsNoTracking()
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(s => 
                s.ClientId.Contains(searchTerm) || 
                s.DisplayName.Contains(searchTerm));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        List<ServiceAccount> items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Update(ServiceAccount serviceAccount)
    {
        this.dbContext.ServiceAccounts.Update(serviceAccount);
    }

    public void Remove(ServiceAccount serviceAccount)
    {
        this.dbContext.ServiceAccounts.Remove(serviceAccount);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // If the concurrency conflict is only on OpenIddict entities, detach and retry once.
            // This prevents unrelated OpenIddict token updates from failing domain writes.
            if (ex.Entries.All(entry => entry.Metadata.ClrType.Namespace?.StartsWith("OpenIddict", StringComparison.Ordinal) == true))
            {
                foreach (EntityEntry entry in ex.Entries)
                {
                    entry.State = EntityState.Detached;
                }

                return await this.dbContext.SaveChangesAsync(cancellationToken);
            }

            throw;
        }
    }
}
