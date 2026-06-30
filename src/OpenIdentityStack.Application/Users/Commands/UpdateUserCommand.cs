using OpenIdentityStack.Domain.Users;
using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Command to update a user's details.
/// </summary>
/// <param name="UserId">The ID of the user to update.</param>
/// <param name="DisplayName">The new display name (optional).</param>
/// <param name="ActorId">The ID of the admin performing the action (for auditing).</param>
/// <param name="Profile">Optional profile fields to update.</param>
public sealed record UpdateUserCommand(
    UserId UserId,
    string? DisplayName,
    string ActorId,
    UserProfileData? Profile = null);

/// <summary>
/// Result of updating a user.
/// </summary>
/// <param name="UserId">The ID of the updated user.</param>
/// <param name="UpdatedAt">When the user was updated.</param>
public sealed record UpdateUserResult(UserId UserId, DateTimeOffset UpdatedAt);

/// <summary>
/// Interface for the update user use case.
/// </summary>
public interface IUpdateUserUseCase
{
    /// <summary>
    /// Updates a user's details.
    /// </summary>
    /// <param name="command">The update user command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the update result or an error.</returns>
    Task<Result<UpdateUserResult>> ExecuteAsync(
        UpdateUserCommand command,
        CancellationToken cancellationToken = default);
}
