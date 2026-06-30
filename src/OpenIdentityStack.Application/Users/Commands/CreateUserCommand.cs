using OpenIdentityStack.Domain.Users;
using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Command to create a new local user.
/// </summary>
/// <param name="Email">The user's email address.</param>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="Password">The user's password (plain text).</param>
/// <param name="ActorId">The ID of the admin performing the action (for auditing).</param>
/// <param name="Profile">Optional profile attributes.</param>
public sealed record CreateUserCommand(
    string Email,
    string DisplayName,
    string Password,
    string ActorId,
    UserProfileData? Profile = null);

/// <summary>
/// Result of creating a user.
/// </summary>
/// <param name="UserId">The created user's ID.</param>
/// <param name="Email">The user's email.</param>
/// <param name="DisplayName">The user's display name.</param>
public sealed record CreateUserResult(
    UserId UserId,
    string Email,
    string DisplayName);

/// <summary>
/// Use case interface for creating a user.
/// </summary>
public interface ICreateUserUseCase
{
    /// <summary>
    /// Creates a new local user.
    /// </summary>
    /// <param name="command">The create user command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    Task<Result<CreateUserResult>> ExecuteAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken = default);
}
