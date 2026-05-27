using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Domain.Tests.Applications;

public sealed class ApplicationIdTests
{
    [Fact]
    public void Create_ReturnsNonEmptyId()
    {
        var id = DomainApplicationId.Create();

        id.ShouldNotBe(DomainApplicationId.Empty);
    }

    [Fact]
    public void NewId_ReturnsNonEmptyId()
    {
        var id = DomainApplicationId.NewId();

        id.ShouldNotBe(DomainApplicationId.Empty);
    }

    [Fact]
    public void Parse_WithValidGuid_ReturnsParsedId()
    {
        var expected = Guid.NewGuid();

        var id = DomainApplicationId.Parse(expected.ToString());

        id.Value.ShouldBe(expected);
        id.ToString().ShouldBe(expected.ToString());
    }

    [Fact]
    public void TryParse_WithValidGuid_ReturnsParsedId()
    {
        var expected = DomainApplicationId.Create();

        bool isParsed = DomainApplicationId.TryParse(expected.ToString(), out DomainApplicationId actual);

        isParsed.ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void TryParse_WithInvalidValue_ReturnsFalse(string? value)
    {
        bool isParsed = DomainApplicationId.TryParse(value, out DomainApplicationId actual);

        isParsed.ShouldBeFalse();
        actual.ShouldBe(DomainApplicationId.Empty);
    }
}
