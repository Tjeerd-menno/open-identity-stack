using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Domain.Tests.ServicePermissions;

public sealed class ServiceOwnerTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingOwnerId_ReturnsValidationError(string ownerId)
    {
        Result<ServiceOwner> result = ServiceOwner.Create(ownerId, OwnerType.Group);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.ServiceOwner.OwnerIdRequired");
    }

    [Fact]
    public void Create_WithValidOwnerId_TrimsValueAndReturnsOwner()
    {
        Result<ServiceOwner> result = ServiceOwner.Create(" owner-1 ", OwnerType.ExternalGroup);

        result.IsSuccess.ShouldBeTrue();
        result.Value.OwnerId.ShouldBe("owner-1");
        result.Value.OwnerType.ShouldBe(OwnerType.ExternalGroup);
    }
}
