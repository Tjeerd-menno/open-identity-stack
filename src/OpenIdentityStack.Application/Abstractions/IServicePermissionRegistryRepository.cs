using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Application.ServicePermissions.Dtos;
using OpenIdentityStack.Application.ServicePermissions.Queries;
using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Application.Abstractions;

public interface IServicePermissionRegistryRepository
{
    Task<bool> ExistsByIdentifierAsync(string serviceIdentifier, CancellationToken cancellationToken = default);

    Task AddAsync(RegisteredService service, CancellationToken cancellationToken = default);

    Task<RegisteredService?> GetByIdAsync(RegisteredServiceId id, CancellationToken cancellationToken = default);

    Task<RegisteredService?> GetByIdentifierAsync(string serviceIdentifier, CancellationToken cancellationToken = default);

    Task<PagedResult<RegisteredServiceSummaryDto>> ListServicesAsync(ListRegisteredServicesQuery query, CancellationToken cancellationToken = default);

    Task<PagedResult<ServicePermission>> ListAssignablePermissionsAsync(ListAssignablePermissionCatalogQuery query, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
