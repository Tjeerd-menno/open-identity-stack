using Microsoft.Extensions.Configuration;

namespace OpenIdentityStack.Infrastructure.Common;

/// <summary>
/// Validates the operator-provided AES key used to protect application-managed secrets.
/// Shared by every entry point that must fail fast on a malformed key.
/// </summary>
public static class SecretEncryptionKey
{
    /// <summary>
    /// Configuration key holding the base64-encoded 256-bit secret encryption key.
    /// </summary>
    public const string ConfigurationKey = "Secrets:EncryptionKey";

    private const int keySizeInBytes = 32;
    private const string invalidKeyMessage = "Secrets:EncryptionKey must be a base64-encoded 256-bit key.";

    /// <summary>
    /// Reads and validates <see cref="ConfigurationKey"/>.
    /// </summary>
    /// <returns>The decoded key, or <see langword="null"/> when no key is configured.</returns>
    /// <exception cref="InvalidOperationException">The configured value is not a base64-encoded 256-bit key.</exception>
    public static byte[]? Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return Parse(configuration[ConfigurationKey]);
    }

    /// <summary>
    /// Validates <see cref="ConfigurationKey"/> without keeping the decoded key.
    /// </summary>
    /// <exception cref="InvalidOperationException">The configured value is not a base64-encoded 256-bit key.</exception>
    public static void Validate(IConfiguration configuration) => _ = Resolve(configuration);

    private static byte[]? Parse(string? configuredKey)
    {
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return null;
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(configuredKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(invalidKeyMessage, exception);
        }

        if (key.Length != keySizeInBytes)
        {
            throw new InvalidOperationException(invalidKeyMessage);
        }

        return key;
    }
}
