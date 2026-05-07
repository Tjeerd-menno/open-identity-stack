using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Queries;

/// <summary>
/// Handler interface for getting group claims for a user.
/// </summary>
public interface IGetGroupClaimsForUserQueryHandler
{
    /// <summary>
    /// Gets all group-derived claims for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the list of group claim DTOs.</returns>
    Task<Result<IReadOnlyList<GroupClaimDto>>> HandleAsync(
        UserId userId,
        CancellationToken cancellationToken = default);
}
