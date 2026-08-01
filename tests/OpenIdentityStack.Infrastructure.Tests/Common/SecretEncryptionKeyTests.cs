using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using OpenIdentityStack.Infrastructure.Common;

namespace OpenIdentityStack.Infrastructure.Tests.Common;

public sealed class SecretEncryptionKeyTests
{
    private const string expectedMessage = "Secrets:EncryptionKey must be a base64-encoded 256-bit key.";

    [Fact]
    public void Resolve_WithValidKey_ReturnsDecodedKey()
    {
        byte[] expected = RandomNumberGenerator.GetBytes(32);
        IConfiguration configuration = BuildConfiguration(Convert.ToBase64String(expected));

        byte[]? key = SecretEncryptionKey.Resolve(configuration);

        key.ShouldBe(expected);
    }

    [Fact]
    public void Resolve_WithNonBase64Key_Throws()
    {
        IConfiguration configuration = BuildConfiguration("not base64!");

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => SecretEncryptionKey.Resolve(configuration));

        exception.Message.ShouldBe(expectedMessage);
    }

    [Fact]
    public void Resolve_WithBase64KeyOfWrongLength_Throws()
    {
        // Regression: a 36-byte key passed through the migrator unnoticed and only broke the API.
        IConfiguration configuration = BuildConfiguration(Convert.ToBase64String(RandomNumberGenerator.GetBytes(36)));

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => SecretEncryptionKey.Resolve(configuration));

        exception.Message.ShouldBe(expectedMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WithoutConfiguredKey_ReturnsNull(string? configuredKey)
    {
        IConfiguration configuration = BuildConfiguration(configuredKey);

        byte[]? key = SecretEncryptionKey.Resolve(configuration);

        key.ShouldBeNull();
    }

    private static IConfiguration BuildConfiguration(string? configuredKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SecretEncryptionKey.ConfigurationKey] = configuredKey
            })
            .Build();
}
