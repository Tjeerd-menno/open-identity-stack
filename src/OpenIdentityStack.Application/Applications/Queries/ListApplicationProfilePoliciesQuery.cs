using SharedKernel;

namespace OpenIdentityStack.Application.Applications.Queries;

public interface IListApplicationProfilePoliciesQueryHandler
{
    Task<Result<IReadOnlyList<ApplicationProfilePolicyDetails>>> HandleAsync(
        CancellationToken cancellationToken = default);
}
