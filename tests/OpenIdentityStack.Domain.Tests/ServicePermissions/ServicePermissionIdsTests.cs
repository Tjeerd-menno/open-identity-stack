using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Domain.Tests.ServicePermissions;

public sealed class ServicePermissionIdsTests
{
    [Fact]
    public void RegisteredServiceId_Create_ReturnsNonEmptyId()
    {
        RegisteredServiceId id = RegisteredServiceId.Create();

        id.ShouldNotBe(RegisteredServiceId.Empty);
    }

    [Fact]
    public void RegisteredServiceId_TryParse_WithValidGuid_ReturnsParsedId()
    {
        RegisteredServiceId expected = RegisteredServiceId.Create();

        bool isParsed = RegisteredServiceId.TryParse(expected.ToString(), out RegisteredServiceId actual);

        isParsed.ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void RegisteredServiceId_TryParse_WithInvalidValue_ReturnsFalse(string? value)
    {
        bool isParsed = RegisteredServiceId.TryParse(value, out RegisteredServiceId actual);

        isParsed.ShouldBeFalse();
        actual.ShouldBe(RegisteredServiceId.Empty);
    }

    [Fact]
    public void ServicePermissionId_Create_ReturnsNonEmptyId()
    {
        ServicePermissionId id = ServicePermissionId.Create();

        id.ShouldNotBe(ServicePermissionId.Empty);
    }

    [Fact]
    public void ServicePermissionId_TryParse_WithValidGuid_ReturnsParsedId()
    {
        ServicePermissionId expected = ServicePermissionId.Create();

        bool isParsed = ServicePermissionId.TryParse(expected.ToString(), out ServicePermissionId actual);

        isParsed.ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void ServicePermissionId_TryParse_WithInvalidValue_ReturnsFalse(string? value)
    {
        bool isParsed = ServicePermissionId.TryParse(value, out ServicePermissionId actual);

        isParsed.ShouldBeFalse();
        actual.ShouldBe(ServicePermissionId.Empty);
    }

    [Fact]
    public void DelegatedMaintainerId_Create_ReturnsNonEmptyId()
    {
        DelegatedMaintainerId id = DelegatedMaintainerId.Create();

        id.ShouldNotBe(DelegatedMaintainerId.Empty);
    }

    [Fact]
    public void DelegatedMaintainerId_TryParse_WithValidGuid_ReturnsParsedId()
    {
        DelegatedMaintainerId expected = DelegatedMaintainerId.Create();

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
