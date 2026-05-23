using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;

namespace OpenIdentityStack.Application.ApplicationPermissions.Queries;

public interface IListRegisteredApplicationsQueryHandler
{
    Task<PagedResult<RegisteredApplicationSummaryDto>> HandleAsync(ListRegisteredApplicationsQuery query, CancellationToken cancellationToken = default);
}

public sealed class ListRegisteredApplicationsQueryHandler : IListRegisteredApplicationsQueryHandler
{
    private readonly IApplicationPermissionRegistryRepository repository;

    public ListRegisteredApplicationsQueryHandler(IApplicationPermissionRegistryRepository repository)
    {
        this.repository = repository;
    }

    public async Task<PagedResult<RegisteredApplicationSummaryDto>> HandleAsync(ListRegisteredApplicationsQuery query, CancellationToken cancellationToken = default)
    {
        return await this.repository.ListApplicationsAsync(query, cancellationToken).ConfigureAwait(false);
    }
}
