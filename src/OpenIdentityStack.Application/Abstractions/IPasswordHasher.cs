namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Service for hashing and verifying passwords.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a password.
    /// </summary>
    /// <param name="password">The plain text password.</param>
    /// <returns>The hashed password.</returns>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a password against a hash.
    /// </summary>
    /// <param name="hashedPassword">The stored password hash.</param>
    /// <param name="providedPassword">The password to verify.</param>
    /// <returns>True if the password matches the hash.</returns>
    bool VerifyPassword(string hashedPassword, string providedPassword);
}
