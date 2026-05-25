using SharedKernel;

namespace OpenIdentityStack.Application.Applications.Queries;

public interface IListApplicationTypePoliciesQueryHandler
{
    Task<Result<IReadOnlyList<ApplicationTypePolicyDetails>>> HandleAsync(
        CancellationToken cancellationToken = default);
}
