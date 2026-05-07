using SharedKernel;
namespace OpenIdentityStack.Application.Roles.Queries;

/// <summary>
/// Handler interface for getting roles assigned to a user.
/// </summary>
public interface IGetUserRolesQueryHandler
{
    /// <summary>
    /// Gets roles assigned to a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="activeOnly">Whether to return only active roles.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the user roles response.</returns>
    Task<Result<GetUserRolesResponse>> HandleAsync(
        UserId userId,
        bool activeOnly = true,
        CancellationToken cancellationToken = default);
}
