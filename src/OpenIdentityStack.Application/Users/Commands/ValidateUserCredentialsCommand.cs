using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Command to validate user credentials.
/// </summary>
/// <param name="Email">The user's email address.</param>
/// <param name="Password">The user's password (plain text).</param>
public sealed record ValidateUserCredentialsCommand(
    string Email,
    string Password);

/// <summary>
/// Result of validating user credentials.
/// </summary>
/// <param name="UserId">The authenticated user's ID.</param>
/// <param name="Email">The user's email.</param>
/// <param name="DisplayName">The user's display name.</param>
public sealed record ValidateUserCredentialsResult(
    UserId UserId,
    string Email,
    string DisplayName);

/// <summary>
/// Use case interface for validating user credentials.
/// </summary>
public interface IValidateUserCredentialsUseCase
{
    /// <summary>
    /// Validates user credentials and returns user information if valid.
    /// </summary>
    /// <param name="command">The validate credentials command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    Task<Result<ValidateUserCredentialsResult>> ExecuteAsync(
        ValidateUserCredentialsCommand command,
        CancellationToken cancellationToken = default);
}
