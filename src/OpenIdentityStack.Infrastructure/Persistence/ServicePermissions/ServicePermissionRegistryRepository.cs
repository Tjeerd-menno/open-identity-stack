using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Application.ServicePermissions.Dtos;
using OpenIdentityStack.Application.ServicePermissions.Queries;
using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Infrastructure.Persistence.ServicePermissions;

public sealed class ServicePermissionRegistryRepository : IServicePermissionRegistryRepository
{
    private readonly OpenIdentityStackDbContext dbContext;

    public ServicePermissionRegistryRepository(OpenIdentityStackDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<bool> ExistsByIdentifierAsync(string serviceIdentifier, CancellationToken cancellationToken = default)
    {
        string normalized = serviceIdentifier.Trim().ToLowerInvariant();
        return await this.dbContext.RegisteredServices
            .AnyAsync(s => s.ServiceIdentifier == normalized, cancellationToken);
    }

    public async Task AddAsync(RegisteredService service, CancellationToken cancellationToken = default)
    {
        await this.dbContext.RegisteredServices.AddAsync(service, cancellationToken);
    }

    public async Task<RegisteredService?> GetByIdAsync(RegisteredServiceId id, CancellationToken cancellationToken = default)
    {
        return await this.dbContext.RegisteredServices
            .Include(s => s.Permissions)
            .Include(s => s.Maintainers)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<RegisteredService?> GetByIdentifierAsync(string serviceIdentifier, CancellationToken cancellationToken = default)
    {
        string normalized = serviceIdentifier.Trim().ToLowerInvariant();
        return await this.dbContext.RegisteredServices
            .Include(s => s.Permissions)
            .Include(s => s.Maintainers)
            .FirstOrDefaultAsync(s => s.ServiceIdentifier == normalized, cancellationToken);
    }

    public async Task<RegisteredService?> GetByPermissionIdAsync(ServicePermissionId permissionId, CancellationToken cancellationToken = default)
    {
        return await this.dbContext.RegisteredServices
            .Include(s => s.Permissions)
            .Include(s => s.Maintainers)
            .FirstOrDefaultAsync(s => s.Permissions.Any(p => p.Id == permissionId), cancellationToken);
    }

    public async Task<ServicePermission?> GetPermissionByFullKeyAsync(string fullPermissionKey, CancellationToken cancellationToken = default)
    {
        string normalized = fullPermissionKey.Trim().ToLowerInvariant();
        return await this.dbContext.ServicePermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.FullPermissionKey == normalized, cancellationToken);
    }

    public async Task<PagedResult<RegisteredServiceSummaryDto>> ListServicesAsync(ListRegisteredServicesQuery query, CancellationToken cancellationToken = default)
    {
        int page = Math.Max(query.Page, 1);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);
        IQueryable<RegisteredService> services = this.dbContext.RegisteredServices.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.StatusFilter) && Enum.TryParse(query.StatusFilter, ignoreCase: true, out ServiceLifecycleStatus status))
        {
            services = services.Where(s => s.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.OwnerFilter))
        {
            string owner = query.OwnerFilter.Trim();
            services = services.Where(s => s.OwnerId == owner);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            string search = $"%{query.SearchTerm.Trim()}%";
            services = services.Where(s => EF.Functions.Like(s.ServiceIdentifier, search) || EF.Functions.Like(s.DisplayName, search));
        }

        int totalCount = await services.CountAsync(cancellationToken);
        List<RegisteredServiceSummaryDto> items = await services
            .OrderBy(s => s.ServiceIdentifier)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new RegisteredServiceSummaryDto(
                s.Id.Value,
                s.ServiceIdentifier,
                s.DisplayName,
                s.OwnerId,
                s.Status.ToString(),
                s.Permissions.Count,
                s.CreatedAt,
                s.ModifiedAt))
            .ToListAsync(cancellationToken);

        return PagedResult<RegisteredServiceSummaryDto>.Create(items, page, pageSize, totalCount);
    }

    public async Task<PagedResult<ServicePermission>> ListAssignablePermissionsAsync(ListAssignablePermissionCatalogQuery query, CancellationToken cancellationToken = default)
    {
        int page = Math.Max(query.Page, 1);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);
        IQueryable<ServicePermission> permissions = this.dbContext.ServicePermissions.AsNoTracking();

        if (query.AssignableOnly)
        {
            permissions = permissions.Where(p => p.IsAssignable);
        }

        if (!string.IsNullOrWhiteSpace(query.ServiceIdentifier))
        {
            string prefix = query.ServiceIdentifier.Trim().ToLowerInvariant() + ":";
            permissions = permissions.Where(p => p.FullPermissionKey.StartsWith(prefix));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            string search = $"%{query.SearchTerm.Trim()}%";
            permissions = permissions.Where(p => EF.Functions.Like(p.FullPermissionKey, search) || EF.Functions.Like(p.DisplayName, search));
        }

        int totalCount = await permissions.CountAsync(cancellationToken);
        List<ServicePermission> items = await permissions
            .OrderBy(p => p.FullPermissionKey)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<ServicePermission>.Create(items, page, pageSize, totalCount);
    }

    public async Task<bool> IsPermissionAssignableAsync(string fullPermissionKey, CancellationToken cancellationToken = default)
    {
        string normalized = fullPermissionKey.Trim().ToLowerInvariant();
        return await this.dbContext.ServicePermissions
            .AnyAsync(p => p.FullPermissionKey == normalized && p.IsAssignable, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await this.dbContext.SaveChangesAsync(cancellationToken);
    }
}
