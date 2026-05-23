using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Domain.Tests.ApplicationPermissions;

public sealed class ApplicationPermissionIdsTests
{
    [Fact]
    public void RegisteredApplicationId_Create_ReturnsNonEmptyId()
    {
        var id = RegisteredApplicationId.Create();

        id.ShouldNotBe(RegisteredApplicationId.Empty);
    }

    [Fact]
    public void RegisteredApplicationId_TryParse_WithValidGuid_ReturnsParsedId()
    {
        var expected = RegisteredApplicationId.Create();

        bool isParsed = RegisteredApplicationId.TryParse(expected.ToString(), out RegisteredApplicationId actual);

        isParsed.ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void RegisteredApplicationId_TryParse_WithInvalidValue_ReturnsFalse(string? value)
    {
        bool isParsed = RegisteredApplicationId.TryParse(value, out RegisteredApplicationId actual);

        isParsed.ShouldBeFalse();
        actual.ShouldBe(RegisteredApplicationId.Empty);
    }

    [Fact]
    public void ApplicationPermissionId_Create_ReturnsNonEmptyId()
    {
        var id = ApplicationPermissionId.Create();

        id.ShouldNotBe(ApplicationPermissionId.Empty);
    }

    [Fact]
    public void ApplicationPermissionId_TryParse_WithValidGuid_ReturnsParsedId()
    {
        var expected = ApplicationPermissionId.Create();

        bool isParsed = ApplicationPermissionId.TryParse(expected.ToString(), out ApplicationPermissionId actual);

        isParsed.ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void ApplicationPermissionId_TryParse_WithInvalidValue_ReturnsFalse(string? value)
    {
        bool isParsed = ApplicationPermissionId.TryParse(value, out ApplicationPermissionId actual);

        isParsed.ShouldBeFalse();
        actual.ShouldBe(ApplicationPermissionId.Empty);
    }

    [Fact]
    public void DelegatedMaintainerId_Create_ReturnsNonEmptyId()
    {
        var id = DelegatedMaintainerId.Create();

        id.ShouldNotBe(DelegatedMaintainerId.Empty);
    }

    [Fact]
    public void DelegatedMaintainerId_TryParse_WithValidGuid_ReturnsParsedId()
    {
        var expected = DelegatedMaintainerId.Create();

        bool isParsed = DelegatedMaintainerId.TryParse(expected.ToString(), out DelegatedMaintainerId actual);

        isParsed.ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void DelegatedMaintainerId_TryParse_WithInvalidValue_ReturnsFalse(string? value)
    {
        bool isParsed = DelegatedMaintainerId.TryParse(value, out DelegatedMaintainerId actual);

        isParsed.ShouldBeFalse();
        actual.ShouldBe(DelegatedMaintainerId.Empty);
    }
}
