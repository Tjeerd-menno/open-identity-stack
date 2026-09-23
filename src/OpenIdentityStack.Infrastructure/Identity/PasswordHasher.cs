using Microsoft.AspNetCore.Identity;
using OpenIdentityStack.Application.Abstractions;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>
/// Password hasher implementation using ASP.NET Core Identity's PasswordHasher.
/// Provides industry-standard password hashing with PBKDF2-HMAC-SHA256.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> hasher = new();
    private static readonly object dummyUser = new();
    private static readonly string dummyHash = new PasswordHasher<object>()
        .HashPassword(dummyUser, Guid.NewGuid().ToString());

    /// <inheritdoc />
    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentNullException(nameof(password));
        }

        return this.hasher.HashPassword(dummyUser, password);
    }

    /// <inheritdoc />
    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword))
        {
            // Spend the normal password-verification work for unknown and passwordless
            // accounts, without allowing the dummy credential to authenticate.
            if (!string.IsNullOrEmpty(providedPassword))
            {
                this.hasher.VerifyHashedPassword(dummyUser, dummyHash, providedPassword);
            }

            return false;
        }

        if (string.IsNullOrEmpty(providedPassword))
        {
            return false;
        }

        PasswordVerificationResult result = this.hasher.VerifyHashedPassword(dummyUser, hashedPassword, providedPassword);
        
        // Accept Success or SuccessRehashNeeded (both mean password is valid)
        return result != PasswordVerificationResult.Failed;
    }
}
