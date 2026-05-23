using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Application.ApplicationPermissions.Queries;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.Abstractions;

public interface IApplicationPermissionRegistryRepository
{
    Task<bool> ExistsByIdentifierAsync(string applicationIdentifier, CancellationToken cancellationToken = default);

    Task AddAsync(RegisteredApplication application, CancellationToken cancellationToken = default);

    Task<RegisteredApplication?> GetByIdAsync(RegisteredApplicationId id, CancellationToken cancellationToken = default);

    Task<RegisteredApplication?> GetByIdentifierAsync(string applicationIdentifier, CancellationToken cancellationToken = default);

    Task<RegisteredApplication?> GetByPermissionIdAsync(ApplicationPermissionId permissionId, CancellationToken cancellationToken = default);

    Task<ApplicationPermission?> GetPermissionByFullKeyAsync(string fullPermissionKey, CancellationToken cancellationToken = default);

    Task<PagedResult<RegisteredApplicationSummaryDto>> ListApplicationsAsync(ListRegisteredApplicationsQuery query, CancellationToken cancellationToken = default);

    Task<PagedResult<ApplicationPermissionDto>> ListAssignablePermissionCatalogAsync(ListAssignablePermissionCatalogQuery query, CancellationToken cancellationToken = default);

    Task<bool> IsPermissionAssignableAsync(string fullPermissionKey, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
