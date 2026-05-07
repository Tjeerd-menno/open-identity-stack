using OpenIdentityStack.Application.Roles.Queries;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Queries;

/// <summary>
/// Handler interface for getting effective roles for a user.
/// </summary>
public interface IGetUserEffectiveRolesQueryHandler
{
    /// <summary>
    /// Gets all effective roles for a user (direct + group-mapped).
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the list of role DTOs.</returns>
    Task<Result<IReadOnlyList<RoleDto>>> HandleAsync(
        UserId userId,
        CancellationToken cancellationToken = default);
}
