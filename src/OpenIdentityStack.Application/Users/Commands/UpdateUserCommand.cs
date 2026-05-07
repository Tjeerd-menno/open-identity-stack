using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Command to update a user's details.
/// </summary>
/// <param name="UserId">The ID of the user to update.</param>
/// <param name="DisplayName">The new display name (optional).</param>
public sealed record UpdateUserCommand(UserId UserId, string? DisplayName);

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
