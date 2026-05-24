using SharedKernel;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications.Queries;

public interface IGetApplicationQueryHandler
{
    Task<Result<ApplicationDetails>> HandleAsync(
        DomainApplicationId applicationId,
        CancellationToken cancellationToken = default);
}
