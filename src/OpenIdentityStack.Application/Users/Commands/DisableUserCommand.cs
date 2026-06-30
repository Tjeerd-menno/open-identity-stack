using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Command to disable a user account.
/// </summary>
/// <param name="UserId">The ID of the user to disable.</param>
/// <param name="Reason">The reason for disabling the user.</param>
/// <param name="ActorId">The ID of the admin performing the action (for auditing).</param>
public sealed record DisableUserCommand(UserId UserId, string Reason, string ActorId);

/// <summary>
/// Result of disabling a user.
/// </summary>
/// <param name="UserId">The ID of the disabled user.</param>
/// <param name="DisabledAt">When the user was disabled.</param>
public sealed record DisableUserResult(UserId UserId, DateTimeOffset DisabledAt);

/// <summary>
/// Interface for the disable user use case.
/// </summary>
public interface IDisableUserUseCase
{
    /// <summary>
    /// Disables a user account.
    /// </summary>
    /// <param name="command">The disable user command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the disable result or an error.</returns>
    Task<Result<DisableUserResult>> ExecuteAsync(
        DisableUserCommand command,
        CancellationToken cancellationToken = default);
}
