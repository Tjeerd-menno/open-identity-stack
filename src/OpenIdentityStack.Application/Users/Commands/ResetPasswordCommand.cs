using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Command to reset a user's password.
/// </summary>
/// <param name="UserId">The ID of the user.</param>
/// <param name="NewPassword">The new password (plain text, will be hashed).</param>
/// <param name="ActorId">The ID of the admin performing the action (for auditing).</param>
public sealed record ResetPasswordCommand(UserId UserId, string NewPassword, string ActorId);

/// <summary>
/// Result of resetting a password.
/// </summary>
/// <param name="UserId">The ID of the user.</param>
/// <param name="ResetAt">When the password was reset.</param>
public sealed record ResetPasswordResult(UserId UserId, DateTimeOffset ResetAt);

/// <summary>
/// Interface for the reset password use case.
/// </summary>
public interface IResetPasswordUseCase
{
    /// <summary>
    /// Resets a user's password.
    /// </summary>
    /// <param name="command">The reset password command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the reset result or an error.</returns>
    Task<Result<ResetPasswordResult>> ExecuteAsync(
        ResetPasswordCommand command,
        CancellationToken cancellationToken = default);
}
