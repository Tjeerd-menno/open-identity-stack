using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Domain.Applications;
using SharedKernel;

namespace OpenIdentityStack.Application.Applications.Queries;

public interface IListApplicationsQueryHandler
{
    Task<Result<PagedResult<ApplicationSummary>>> HandleAsync(
        int page = 1,
        int pageSize = 20,
        ApplicationProfile? profile = null,
        ApplicationStatus? status = null,
        OAuthClientType? clientType = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default);
}
