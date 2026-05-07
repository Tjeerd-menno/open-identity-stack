using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OpenIdentityStack.Application.Abstractions;

namespace OpenIdentityStack.Infrastructure.Common;

/// <summary>
/// Encrypts application-managed secrets using an operator-provided AES key.
/// </summary>
public sealed class AesSecretProtector : ISecretProtector
{
    private const string prefix = "enc:v1:";
    private const int nonceSize = 12;
    private const int tagSize = 16;
    private readonly byte[]? key;
    private readonly bool allowPlainTextFallback;

    public AesSecretProtector(IConfiguration configuration, IHostEnvironment environment)
    {
        string? configuredKey = configuration["Secrets:EncryptionKey"];
        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            this.key = Convert.FromBase64String(configuredKey);
            if (this.key.Length != 32)
            {
                throw new InvalidOperationException("Secrets:EncryptionKey must be a base64-encoded 256-bit key.");
            }
        }

        this.allowPlainTextFallback = environment.IsDevelopment() || environment.IsEnvironment("Testing");
    }

    public string Protect(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("Secret must not be empty.", nameof(secret));
        }

        if (secret.StartsWith(prefix, StringComparison.Ordinal))
        {
            return secret;
        }

        if (this.key is null)
        {
            if (this.allowPlainTextFallback)
            {
                return secret;
            }

            throw new InvalidOperationException("Secrets:EncryptionKey must be configured before storing provider secrets.");
        }

        byte[] nonce = RandomNumberGenerator.GetBytes(nonceSize);
        byte[] plaintext = System.Text.Encoding.UTF8.GetBytes(secret);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[tagSize];

        using var aes = new AesGcm(this.key, tagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        byte[] payload = new byte[nonceSize + tagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonceSize);
        Buffer.BlockCopy(tag, 0, payload, nonceSize, tagSize);
        Buffer.BlockCopy(ciphertext, 0, payload, nonceSize + tagSize, ciphertext.Length);

        return prefix + Convert.ToBase64String(payload);
    }

    public string? Unprotect(string? protectedSecret)
    {
        if (string.IsNullOrWhiteSpace(protectedSecret))
        {
            return null;
        }

        if (!protectedSecret.StartsWith(prefix, StringComparison.Ordinal))
        {
            return protectedSecret;
        }

        if (this.key is null)
        {
            throw new InvalidOperationException("Secrets:EncryptionKey must be configured before reading provider secrets.");
        }

        byte[] payload = Convert.FromBase64String(protectedSecret[prefix.Length..]);
        if (payload.Length < nonceSize + tagSize)
        {
            throw new InvalidOperationException("Encrypted secret payload is invalid.");
        }

        byte[] nonce = payload[..nonceSize];
        byte[] tag = payload[nonceSize..(nonceSize + tagSize)];
        byte[] ciphertext = payload[(nonceSize + tagSize)..];
        byte[] plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(this.key, tagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return System.Text.Encoding.UTF8.GetString(plaintext);
    }
}
