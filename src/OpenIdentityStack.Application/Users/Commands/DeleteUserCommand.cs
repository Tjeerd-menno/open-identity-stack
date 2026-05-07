using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Command to delete a user.
/// </summary>
/// <param name="UserId">The ID of the user to delete.</param>
public sealed record DeleteUserCommand(UserId UserId);

/// <summary>
/// Interface for the delete user use case.
/// </summary>
public interface IDeleteUserUseCase
{
    /// <summary>
    /// Deletes a user.
    /// </summary>
    /// <param name="command">The delete user command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or an error.</returns>
    Task<Result> ExecuteAsync(
        DeleteUserCommand command,
        CancellationToken cancellationToken = default);
}
