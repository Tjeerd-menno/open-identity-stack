using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Interface for validating password policies.
/// </summary>
public interface IPasswordPolicyValidator
{
    /// <summary>
    /// Validates that a password meets the configured policy requirements.
    /// </summary>
    /// <param name="password">The plain text password to validate.</param>
    /// <returns>A result indicating success or failure with error details.</returns>
    Result ValidatePassword(string password);
}
