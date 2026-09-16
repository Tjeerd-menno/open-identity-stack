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

        bool hasUppercase = false;
        bool hasLowercase = false;
        bool hasDigit = false;
        bool hasSpecialCharacter = false;

        foreach (char character in password)
        {
            hasUppercase |= char.IsUpper(character);
            hasLowercase |= char.IsLower(character);
            hasDigit |= char.IsDigit(character);
            hasSpecialCharacter |= !char.IsLetterOrDigit(character);

            if (hasUppercase && hasLowercase && hasDigit && hasSpecialCharacter)
            {
                break;
            }
        }

        // Characters are classified in a single pass; the check order below is what determines the reported error.
        if (!hasUppercase)
        {
            return DomainError.Validation(
                "Password.NoUppercase",
                "Password must contain at least one uppercase letter.");
        }

        if (!hasLowercase)
        {
            return DomainError.Validation(
                "Password.NoLowercase",
                "Password must contain at least one lowercase letter.");
        }

        if (!hasDigit)
        {
            return DomainError.Validation(
                "Password.NoDigit",
                "Password must contain at least one number.");
        }

        if (!hasSpecialCharacter)
        {
            return DomainError.Validation(
                "Password.NoSpecialCharacter",
                "Password must contain at least one special character.");
        }

        return Result.Success();
    }
}
