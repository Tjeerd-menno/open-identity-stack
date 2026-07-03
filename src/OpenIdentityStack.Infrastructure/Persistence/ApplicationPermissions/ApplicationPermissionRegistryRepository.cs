using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Application.ApplicationPermissions.Commands;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Application.ApplicationPermissions.Queries;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Infrastructure.Persistence.ApplicationPermissions;

public sealed class ApplicationPermissionRegistryRepository : IApplicationPermissionRegistryRepository
{
    private readonly OpenIdentityStackDbContext dbContext;

    public ApplicationPermissionRegistryRepository(OpenIdentityStackDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<bool> ExistsByIdentifierAsync(string applicationIdentifier, CancellationToken cancellationToken = default)
    {
        string normalized = applicationIdentifier.Trim().ToLowerInvariant();
        return await this.dbContext.RegisteredApplications
            .AnyAsync(s => s.ApplicationIdentifier == normalized && s.DeletedAt == null, cancellationToken);
    }

    public async Task<bool> ExistsByManifestBaseUrlAsync(string manifestBaseUrl, CancellationToken cancellationToken = default)
    {
        string normalized = manifestBaseUrl.Trim().TrimEnd('/');
        return await this.dbContext.RegisteredApplications
            .AnyAsync(s => s.ManifestBaseUrl == normalized && s.DeletedAt == null, cancellationToken);
    }

    public async Task AddAsync(RegisteredApplication application, CancellationToken cancellationToken = default)
    {
        await this.dbContext.RegisteredApplications.AddAsync(application, cancellationToken);
    }

    public async Task<ApplicationPermissionHistoryDto> ListHistoryAsync(
        string? applicationIdentifier,
        bool includeApplications,
        bool includePermissions,
        CancellationToken cancellationToken = default)
    {
        string? normalizedIdentifier = string.IsNullOrWhiteSpace(applicationIdentifier)
            ? null
            : applicationIdentifier.Trim().ToLowerInvariant();

        IQueryable<RegisteredApplication> applicationsQuery = this.dbContext.RegisteredApplications
            .AsNoTracking()
            .Include(application => application.Permissions);

        if (!string.IsNullOrEmpty(normalizedIdentifier))
        {
            applicationsQuery = applicationsQuery.Where(application => application.ApplicationIdentifier == normalizedIdentifier);
        }

        List<RegisteredApplication> applications = await applicationsQuery
            .OrderBy(application => application.ApplicationIdentifier)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<DeletedApplicationHistoryDto> deletedApplications = includeApplications
            ? applications
                .Where(application => application.IsDeleted)
                .Select(ApplicationPermissionMaintenanceUseCases.MapSummary)
                .ToList()
            : [];

        List<RemovedPermissionDetailDto> removedPermissions = includePermissions
            ? applications
                .SelectMany(application => application.Permissions
                    .Where(permission => permission.IsRemoved)
                    .Select(permission => ApplicationPermissionMaintenanceUseCases.MapRemovedPermission(application, permission)))
                .OrderBy(permission => permission.ApplicationIdentifier, StringComparer.OrdinalIgnoreCase)
                .ThenBy(permission => permission.PermissionKey, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

        return new ApplicationPermissionHistoryDto(deletedApplications, removedPermissions);
    }

    public async Task<RegisteredApplication?> GetByIdAsync(RegisteredApplicationId id, CancellationToken cancellationToken = default)
    {
        return await this.dbContext.RegisteredApplications
            .Include(s => s.Permissions)
            .Include(s => s.Maintainers)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<RegisteredApplication?> GetByIdentifierAsync(string applicationIdentifier, CancellationToken cancellationToken = default)
    {
        string normalized = applicationIdentifier.Trim().ToLowerInvariant();
        return await this.dbContext.RegisteredApplications
            .Include(s => s.Permissions)
            .Include(s => s.Maintainers)
            .FirstOrDefaultAsync(s => s.ApplicationIdentifier == normalized, cancellationToken);
    }

    public async Task<RegisteredApplication?> GetByPermissionIdAsync(ApplicationPermissionId permissionId, CancellationToken cancellationToken = default)
    {
        return await this.dbContext.RegisteredApplications
            .Include(s => s.Permissions)
            .Include(s => s.Maintainers)
            .FirstOrDefaultAsync(s => s.Permissions.Any(p => p.Id == permissionId), cancellationToken);
    }

    public async Task<ApplicationPermission?> GetPermissionByFullKeyAsync(string fullPermissionKey, CancellationToken cancellationToken = default)
    {
        string normalized = fullPermissionKey.Trim().ToLowerInvariant();
        return await this.dbContext.ApplicationPermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.FullPermissionKey == normalized && p.RemovedAt == null, cancellationToken);
    }

    public async Task<PagedResult<RegisteredApplicationSummaryDto>> ListApplicationsAsync(ListRegisteredApplicationsQuery query, CancellationToken cancellationToken = default)
    {
        int page = Math.Max(query.Page, 1);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);
        IQueryable<RegisteredApplication> applications = this.dbContext.RegisteredApplications
            .AsNoTracking()
            .Where(application => application.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(query.StatusFilter) && Enum.TryParse(query.StatusFilter, ignoreCase: true, out ApplicationLifecycleStatus status))
        {
            applications = applications.Where(application => application.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.OwnerFilter))
        {
            string owner = query.OwnerFilter.Trim();
            applications = applications.Where(application => application.OwnerId == owner);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            string search = $"%{query.SearchTerm.Trim()}%";
            applications = applications.Where(application =>
                EF.Functions.Like(application.ApplicationIdentifier, search)
                || EF.Functions.Like(application.DisplayName, search));
        }

        int totalCount = await applications.CountAsync(cancellationToken);
        List<RegisteredApplicationSummaryDto> items = await applications
            .OrderBy(application => application.ApplicationIdentifier)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(application => new RegisteredApplicationSummaryDto(
                application.Id.Value,
                application.ApplicationIdentifier,
                application.DisplayName,
                application.OwnerId,
                application.Status.ToString(),
                application.Permissions.Count(permission => permission.RemovedAt == null),
                application.CreatedAt,
                application.ModifiedAt))
            .ToListAsync(cancellationToken);

        return PagedResult<RegisteredApplicationSummaryDto>.Create(items, page, pageSize, totalCount);
    }

    public async Task<IReadOnlyList<ApplicationPermissionDto>> ListAssignablePermissionCatalogAsync(ListAssignablePermissionCatalogQuery query, CancellationToken cancellationToken = default)
    {
        var catalog = this.dbContext.ApplicationPermissions
            .AsNoTracking()
            .Join(
                this.dbContext.RegisteredApplications.AsNoTracking(),
                permission => permission.RegisteredApplicationId,
                application => application.Id,
                (permission, application) => new { Permission = permission, Application = application });

        catalog = catalog.Where(item =>
            item.Application.Status == ApplicationLifecycleStatus.Active
            && item.Application.DeletedAt == null
            && item.Permission.RemovedAt == null);

        if (!string.IsNullOrWhiteSpace(query.ApplicationIdentifier))
        {
            string identifier = query.ApplicationIdentifier.Trim().ToLowerInvariant();
            catalog = catalog.Where(item => item.Application.ApplicationIdentifier == identifier);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            string search = $"%{query.SearchTerm.Trim()}%";
            catalog = catalog.Where(item =>
                EF.Functions.Like(item.Permission.FullPermissionKey, search)
                || EF.Functions.Like(item.Permission.DisplayName, search)
                || EF.Functions.Like(item.Application.DisplayName, search));
        }

        return await catalog
            .OrderBy(item => item.Application.DisplayName)
            .ThenBy(item => item.Permission.FullPermissionKey)
            .Select(item => new ApplicationPermissionDto(
                item.Permission.Id.Value,
                item.Permission.PermissionKey,
                item.Permission.FullPermissionKey,
                item.Permission.DisplayName,
                item.Permission.Description,
                item.Permission.Category,
                item.Permission.CreatedAt,
                item.Permission.ModifiedAt,
                item.Application.ApplicationIdentifier,
                item.Application.DisplayName,
                item.Application.ManifestVersion,
                "concrete",
                true,
                null,
                null))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsPermissionAssignableAsync(string fullPermissionKey, CancellationToken cancellationToken = default)
    {
        string normalized = fullPermissionKey.Trim().ToLowerInvariant();
        return await this.dbContext.ApplicationPermissions
            .Join(
                this.dbContext.RegisteredApplications,
                permission => permission.RegisteredApplicationId,
                application => application.Id,
                (permission, application) => new { Permission = permission, Application = application })
            .AnyAsync(
                item => item.Permission.FullPermissionKey == normalized
                    && item.Application.Status == ApplicationLifecycleStatus.Active
                    && item.Application.DeletedAt == null
                    && item.Permission.RemovedAt == null,
                cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await this.dbContext.SaveChangesAsync(cancellationToken);
    }
}
