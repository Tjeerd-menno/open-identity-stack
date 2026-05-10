using OpenIdentityStack.Domain.Clients;

namespace OpenIdentityStack.Domain.Tests.Clients;

public sealed class ClientIdTests
{
    [Fact]
    public void Create_ReturnsNonEmptyId()
    {
        var id = ClientId.Create();

        id.ShouldNotBe(ClientId.Empty);
    }

    [Fact]
    public void NewId_ReturnsNonEmptyId()
    {
        var id = ClientId.NewId();

        id.ShouldNotBe(ClientId.Empty);
    }

    [Fact]
    public void Parse_WithValidGuid_ReturnsParsedId()
    {
        var expected = Guid.NewGuid();

        var id = ClientId.Parse(expected.ToString());

        id.Value.ShouldBe(expected);
        id.ToString().ShouldBe(expected.ToString());
    }

    [Fact]
    public void TryParse_WithValidGuid_ReturnsParsedId()
    {
        var expected = ClientId.Create();

        bool isParsed = ClientId.TryParse(expected.ToString(), out ClientId actual);

        isParsed.ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void TryParse_WithInvalidValue_ReturnsFalse(string? value)
    {
        bool isParsed = ClientId.TryParse(value, out ClientId actual);

        isParsed.ShouldBeFalse();
        actual.ShouldBe(ClientId.Empty);
    }
}
