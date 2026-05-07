
using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Domain.Tests.Common;
/// <summary>
/// Unit tests for ProviderId strongly-typed ID.
/// </summary>
public sealed class ProviderIdTests
{
    [Fact]
    public void Constructor_WithGuid_SetsValue()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var providerId = new ProviderId(guid);

        // Assert
        providerId.Value.ShouldBe(guid);
    }

    [Fact]
    public void Empty_ReturnsEmptyGuid()
    {
        // Act
        ProviderId providerId = ProviderId.Empty;

        // Assert
        providerId.Value.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void Create_ReturnsNewGuid()
    {
        // Act
        var providerId = ProviderId.Create();

        // Assert
        providerId.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_ReturnsUniqueValues()
    {
        // Act
        var id1 = ProviderId.Create();
        var id2 = ProviderId.Create();

        // Assert
        id1.ShouldNotBe(id2);
    }

    [Fact]
    public void TryParse_WithValidGuidString_ReturnsTrue()
    {
        // Arrange
        var guid = Guid.NewGuid();
        string guidString = guid.ToString();

        // Act
        bool result = ProviderId.TryParse(guidString, out ProviderId providerId);

        // Assert
        result.ShouldBeTrue();
        providerId.Value.ShouldBe(guid);
    }

    [Fact]
    public void TryParse_WithInvalidString_ReturnsFalse()
    {
        // Arrange
        string invalidString = "not-a-guid";

        // Act
        bool result = ProviderId.TryParse(invalidString, out ProviderId providerId);

        // Assert
        result.ShouldBeFalse();
        providerId.ShouldBe(ProviderId.Empty);
    }

    [Fact]
    public void TryParse_WithNull_ReturnsFalse()
    {
        // Act
        bool result = ProviderId.TryParse(null, out ProviderId providerId);

        // Assert
        result.ShouldBeFalse();
        providerId.ShouldBe(ProviderId.Empty);
    }

    [Fact]
    public void TryParse_WithEmptyString_ReturnsFalse()
    {
        // Act
        bool result = ProviderId.TryParse(string.Empty, out ProviderId providerId);

        // Assert
        result.ShouldBeFalse();
        providerId.ShouldBe(ProviderId.Empty);
    }

    [Fact]
    public void ToString_ReturnsGuidString()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var providerId = new ProviderId(guid);

        // Act
        string result = providerId.ToString();

        // Assert
        result.ShouldBe(guid.ToString());
    }

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new ProviderId(guid);
        var id2 = new ProviderId(guid);

        // Assert
        id1.ShouldBe(id2);
        (id1 == id2).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        // Arrange
        var id1 = ProviderId.Create();
        var id2 = ProviderId.Create();

        // Assert
        id1.ShouldNotBe(id2);
        (id1 != id2).ShouldBeTrue();
    }

    [Fact]
    public void GetHashCode_SameValue_ReturnsSameHash()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new ProviderId(guid);
        var id2 = new ProviderId(guid);

        // Assert
        id1.GetHashCode().ShouldBe(id2.GetHashCode());
    }

    [Fact]
    public void CanBeUsedAsDictionaryKey()
    {
        // Arrange
        var id = ProviderId.Create();
        var dictionary = new Dictionary<ProviderId, string>
        {
            { id, "test" }
        };

        // Act & Assert
        dictionary.ContainsKey(id).ShouldBeTrue();
        dictionary[id].ShouldBe("test");
    }
}
