using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Application.ServicePermissions.Dtos;
using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Application.ServicePermissions.Queries;

public interface IListAssignablePermissionCatalogQueryHandler
{
    Task<PagedResult<ServicePermissionDto>> HandleAsync(ListAssignablePermissionCatalogQuery query, CancellationToken cancellationToken = default);
}

public sealed class ListAssignablePermissionCatalogQueryHandler : IListAssignablePermissionCatalogQueryHandler
{
    private readonly IServicePermissionRegistryRepository repository;

    public ListAssignablePermissionCatalogQueryHandler(IServicePermissionRegistryRepository repository)
    {
        this.repository = repository;
    }

    public async Task<PagedResult<ServicePermissionDto>> HandleAsync(ListAssignablePermissionCatalogQuery query, CancellationToken cancellationToken = default)
    {
        PagedResult<ServicePermission> result = await this.repository.ListAssignablePermissionsAsync(query, cancellationToken).ConfigureAwait(false);
        var dtos = result.Items.Select(p => new ServicePermissionDto(
            p.Id.Value,
            p.PermissionKey,
            p.FullPermissionKey,
            p.DisplayName,
            p.Description,
            p.IntendedUse,
            p.DocumentationUrl,
            p.Status.ToString(),
            p.IsAssignable,
            p.CreatedAt,
            p.ModifiedAt)).ToList();
        return PagedResult<ServicePermissionDto>.Create(dtos, result.Page, result.PageSize, result.TotalCount);
    }
}
