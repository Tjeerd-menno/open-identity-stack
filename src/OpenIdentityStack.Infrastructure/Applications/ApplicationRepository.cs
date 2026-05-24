using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Infrastructure.Persistence;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Infrastructure.Applications;

/// <summary>
/// EF Core implementation of application aggregate persistence.
/// </summary>
public sealed class ApplicationRepository : IApplicationRepository
{
    private readonly OpenIdentityStackDbContext dbContext;

    public ApplicationRepository(OpenIdentityStackDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<DomainApplication?> GetByIdAsync(
        DomainApplicationId id,
        CancellationToken cancellationToken = default)
    {
        return await this.dbContext.Applications
            .Include(application => application.Credentials)
            .FirstOrDefaultAsync(application => application.Id == id, cancellationToken);
    }

    public async Task<DomainApplication?> GetByClientIdAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        return await this.dbContext.Applications
            .Include(application => application.Credentials)
            .FirstOrDefaultAsync(application => application.ClientId == clientId, cancellationToken);
    }

    public async Task<bool> ExistsByClientIdAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        return await this.dbContext.Applications
            .AnyAsync(application => application.ClientId == clientId, cancellationToken);
    }

    public async Task<(List<DomainApplication> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        ApplicationType? type,
        ApplicationStatus? status,
        OAuthClientType? clientType,
        string? searchTerm,
        CancellationToken cancellationToken = default)
    {
        IQueryable<DomainApplication> query = this.dbContext.Applications
            .AsNoTracking()
            .AsQueryable();

        if (type.HasValue)
        {
            query = query.Where(application => application.Type == type.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(application => application.Status == status.Value);
        }

        if (clientType.HasValue)
        {
            query = query.Where(application => application.ClientType == clientType.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            string pattern = $"%{searchTerm.Trim()}%";
            query = query.Where(application =>
                EF.Functions.Like(application.ClientId, pattern) ||
                EF.Functions.Like(application.DisplayName, pattern));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        List<DomainApplication> items = await query
            .OrderByDescending(application => application.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(DomainApplication application, CancellationToken cancellationToken = default)
    {
        await this.dbContext.Applications.AddAsync(application, cancellationToken);
    }

    public void Update(DomainApplication application)
    {
        this.dbContext.Applications.Update(application);
    }

    public void Remove(DomainApplication application)
    {
        this.dbContext.Applications.Remove(application);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await this.dbContext.SaveChangesAsync(cancellationToken);
    }
}
