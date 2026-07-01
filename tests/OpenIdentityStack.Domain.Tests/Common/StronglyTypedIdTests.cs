using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Common;

public sealed class StronglyTypedIdTests
{
    [Fact]
    public void UserId_Create_ReturnsNonEmpty()
    {
        var id = UserId.Create();

        id.ShouldNotBe(UserId.Empty);
        id.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void UserId_TryParse_ValidString_ReturnsTrue()
    {
        var guid = Guid.NewGuid();

        UserId.TryParse(guid.ToString(), out UserId result).ShouldBeTrue();
        result.ShouldBe(new UserId(guid));
    }

    [Fact]
    public void UserId_TryParse_InvalidString_ReturnsFalse()
    {
        UserId.TryParse("not-a-guid", out UserId result).ShouldBeFalse();
        result.ShouldBe(UserId.Empty);
    }

    [Fact]
    public void UserId_TryParse_Null_ReturnsFalse()
    {
        UserId.TryParse(null, out UserId result).ShouldBeFalse();
        result.ShouldBe(UserId.Empty);
    }

    [Fact]
    public void UserId_ToString_ReturnsGuidString()
    {
        var guid = Guid.NewGuid();
        var id = new UserId(guid);

        id.ToString().ShouldBe(guid.ToString());
    }

    [Fact]
    public void RoleId_Create_ReturnsNonEmpty()
    {
        var id = RoleId.Create();

        id.ShouldNotBe(RoleId.Empty);
        id.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void RoleId_TryParse_ValidString_ReturnsTrue()
    {
        var guid = Guid.NewGuid();

        RoleId.TryParse(guid.ToString(), out RoleId result).ShouldBeTrue();
        result.ShouldBe(new RoleId(guid));
    }

    [Fact]
    public void RoleId_TryParse_InvalidString_ReturnsFalse()
    {
        RoleId.TryParse("not-a-guid", out RoleId result).ShouldBeFalse();
        result.ShouldBe(RoleId.Empty);
    }

    [Fact]
    public void RoleId_TryParse_Null_ReturnsFalse()
    {
        RoleId.TryParse(null, out RoleId result).ShouldBeFalse();
        result.ShouldBe(RoleId.Empty);
    }

    [Fact]
    public void RoleId_ToString_ReturnsGuidString()
    {
        var guid = Guid.NewGuid();
        var id = new RoleId(guid);

        id.ToString().ShouldBe(guid.ToString());
    }

    [Fact]
    public void GroupId_Create_ReturnsNonEmpty()
    {
        var id = GroupId.Create();

        id.ShouldNotBe(GroupId.Empty);
        id.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void GroupId_TryParse_ValidString_ReturnsTrue()
    {
        var guid = Guid.NewGuid();

        GroupId.TryParse(guid.ToString(), out GroupId result).ShouldBeTrue();
        result.ShouldBe(new GroupId(guid));
    }

    [Fact]
    public void GroupId_TryParse_InvalidString_ReturnsFalse()
    {
        GroupId.TryParse("not-a-guid", out GroupId result).ShouldBeFalse();
        result.ShouldBe(GroupId.Empty);
    }

    [Fact]
    public void GroupId_TryParse_Null_ReturnsFalse()
    {
        GroupId.TryParse(null, out GroupId result).ShouldBeFalse();
        result.ShouldBe(GroupId.Empty);
    }

    [Fact]
    public void GroupId_ToString_ReturnsGuidString()
    {
        var guid = Guid.NewGuid();
        var id = new GroupId(guid);

        id.ToString().ShouldBe(guid.ToString());
    }

    [Fact]
    public void SessionId_Create_ReturnsNonEmpty()
    {
        var id = SessionId.Create();

        id.ShouldNotBe(SessionId.Empty);
        id.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void SessionId_TryParse_ValidString_ReturnsTrue()
    {
        var guid = Guid.NewGuid();

        SessionId.TryParse(guid.ToString(), out SessionId result).ShouldBeTrue();
        result.ShouldBe(new SessionId(guid));
    }

    [Fact]
    public void SessionId_TryParse_InvalidString_ReturnsFalse()
    {
        SessionId.TryParse("not-a-guid", out SessionId result).ShouldBeFalse();
        result.ShouldBe(SessionId.Empty);
    }

    [Fact]
    public void SessionId_TryParse_Null_ReturnsFalse()
    {
        SessionId.TryParse(null, out SessionId result).ShouldBeFalse();
        result.ShouldBe(SessionId.Empty);
    }

    [Fact]
    public void SessionId_ToString_ReturnsGuidString()
    {
        var guid = Guid.NewGuid();
        var id = new SessionId(guid);

        id.ToString().ShouldBe(guid.ToString());
    }
}
