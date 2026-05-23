using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Domain.Tests.ApplicationPermissions;

public sealed class ApplicationOwnerTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingOwnerId_ReturnsValidationError(string ownerId)
    {
        Result<ApplicationOwner> result = ApplicationOwner.Create(ownerId, OwnerType.Group);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.ApplicationOwner.OwnerIdRequired");
    }

    [Fact]
    public void Create_WithValidOwnerId_TrimsValueAndReturnsOwner()
    {
        Result<ApplicationOwner> result = ApplicationOwner.Create(" owner-1 ", OwnerType.ExternalGroup);

        result.IsSuccess.ShouldBeTrue();
        result.Value.OwnerId.ShouldBe("owner-1");
        result.Value.OwnerType.ShouldBe(OwnerType.ExternalGroup);
    }
}
