using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
namespace OpenIdentityStack.Application.ApplicationPermissions.Queries;

public interface IListAssignablePermissionCatalogQueryHandler
{
    Task<PagedResult<ApplicationPermissionDto>> HandleAsync(ListAssignablePermissionCatalogQuery query, CancellationToken cancellationToken = default);
}

public sealed class ListAssignablePermissionCatalogQueryHandler : IListAssignablePermissionCatalogQueryHandler
{
    private readonly IApplicationPermissionRegistryRepository repository;

    public ListAssignablePermissionCatalogQueryHandler(IApplicationPermissionRegistryRepository repository)
    {
        this.repository = repository;
    }

    public async Task<PagedResult<ApplicationPermissionDto>> HandleAsync(ListAssignablePermissionCatalogQuery query, CancellationToken cancellationToken = default)
    {
        return await this.repository.ListAssignablePermissionCatalogAsync(query, cancellationToken).ConfigureAwait(false);
    }
}
