using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Application.ApplicationPermissions.Queries;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.Abstractions;

public interface IApplicationPermissionRegistryRepository
{
    Task<bool> ExistsByIdentifierAsync(string applicationIdentifier, CancellationToken cancellationToken = default);

    Task<bool> ExistsByManifestBaseUrlAsync(string manifestBaseUrl, CancellationToken cancellationToken = default);

    Task AddAsync(RegisteredApplication application, CancellationToken cancellationToken = default);

    Task<ApplicationPermissionHistoryDto> ListHistoryAsync(
        string? applicationIdentifier,
        bool includeApplications,
        bool includePermissions,
        CancellationToken cancellationToken = default);

    Task<RegisteredApplication?> GetByIdAsync(RegisteredApplicationId id, CancellationToken cancellationToken = default);

    Task<RegisteredApplication?> GetByIdentifierAsync(string applicationIdentifier, CancellationToken cancellationToken = default);

    Task<RegisteredApplication?> GetByPermissionIdAsync(ApplicationPermissionId permissionId, CancellationToken cancellationToken = default);

    Task<ApplicationPermission?> GetPermissionByFullKeyAsync(string fullPermissionKey, CancellationToken cancellationToken = default);

    Task<PagedResult<RegisteredApplicationSummaryDto>> ListApplicationsAsync(ListRegisteredApplicationsQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns concrete assignable permissions that match the query filters.
    /// </summary>
    /// <remarks>
    /// This repository boundary does not derive wildcard permissions and does not apply
    /// <see cref="ListAssignablePermissionCatalogQuery.Page"/> or
    /// <see cref="ListAssignablePermissionCatalogQuery.PageSize"/>. Wildcard derivation,
    /// final ordering, and paging are composed by the Application query handler.
    /// </remarks>
    Task<IReadOnlyList<ApplicationPermissionDto>> ListAssignablePermissionCatalogAsync(ListAssignablePermissionCatalogQuery query, CancellationToken cancellationToken = default);

    Task<bool> IsPermissionAssignableAsync(string fullPermissionKey, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
