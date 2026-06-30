using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Command to enable a disabled user account.
/// </summary>
/// <param name="UserId">The ID of the user to enable.</param>
/// <param name="ActorId">The ID of the admin performing the action (for auditing).</param>
public sealed record EnableUserCommand(UserId UserId, string ActorId);

/// <summary>
/// Result of enabling a user.
/// </summary>
/// <param name="UserId">The ID of the enabled user.</param>
/// <param name="EnabledAt">When the user was enabled.</param>
public sealed record EnableUserResult(UserId UserId, DateTimeOffset EnabledAt);

/// <summary>
/// Interface for the enable user use case.
/// </summary>
public interface IEnableUserUseCase
{
    /// <summary>
    /// Enables a disabled user account.
    /// </summary>
    /// <param name="command">The enable user command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the enable result or an error.</returns>
    Task<Result<EnableUserResult>> ExecuteAsync(
        EnableUserCommand command,
        CancellationToken cancellationToken = default);
}
