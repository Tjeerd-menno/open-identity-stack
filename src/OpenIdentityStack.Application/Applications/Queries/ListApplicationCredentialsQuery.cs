using SharedKernel;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications.Queries;

public sealed record ListApplicationCredentialsQuery(DomainApplicationId ApplicationId);

public interface IListApplicationCredentialsQueryHandler
{
    Task<Result<IReadOnlyList<ApplicationCredentialDetails>>> HandleAsync(
        ListApplicationCredentialsQuery query,
        CancellationToken cancellationToken = default);
}

