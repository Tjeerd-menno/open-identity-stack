using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Application.ServicePermissions.Dtos;

namespace OpenIdentityStack.Application.ServicePermissions.Queries;

public interface IListRegisteredServicesQueryHandler
{
    Task<PagedResult<RegisteredServiceSummaryDto>> HandleAsync(ListRegisteredServicesQuery query, CancellationToken cancellationToken = default);
}

public sealed class ListRegisteredServicesQueryHandler : IListRegisteredServicesQueryHandler
{
    private readonly IServicePermissionRegistryRepository repository;

    public ListRegisteredServicesQueryHandler(IServicePermissionRegistryRepository repository)
    {
        this.repository = repository;
    }

    public async Task<PagedResult<RegisteredServiceSummaryDto>> HandleAsync(ListRegisteredServicesQuery query, CancellationToken cancellationToken = default)
    {
        return await this.repository.ListServicesAsync(query, cancellationToken).ConfigureAwait(false);
    }
}
