using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>
/// Password policy validator that enforces security best practices.
/// </summary>
public sealed class PasswordPolicyValidator : IPasswordPolicyValidator
{
    private const int minimumLength = 12;

    /// <inheritdoc />
    public Result ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return DomainError.Validation(
                "Password.Required",
                "Password is required.");
        }

        if (password.Length < minimumLength)
        {
            return DomainError.Validation(
                "Password.TooShort",
                $"Password must be at least {minimumLength} characters long.");
        }

        // Check for at least one uppercase letter
        if (!password.Any(char.IsUpper))
        {
            return DomainError.Validation(
                "Password.NoUppercase",
                "Password must contain at least one uppercase letter.");
        }

        // Check for at least one lowercase letter
        if (!password.Any(char.IsLower))
        {
            return DomainError.Validation(
                "Password.NoLowercase",
                "Password must contain at least one lowercase letter.");
        }

        // Check for at least one digit
        if (!password.Any(char.IsDigit))
        {
            return DomainError.Validation(
                "Password.NoDigit",
                "Password must contain at least one number.");
        }

        // Check for at least one special character
        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            return DomainError.Validation(
                "Password.NoSpecialCharacter",
                "Password must contain at least one special character.");
        }

        return Result.Success();
    }
}
