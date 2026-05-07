using OpenIdentityStack.Domain.Groups;

namespace OpenIdentityStack.Domain.Tests.Groups;

public class GroupMappingTests
{
    [Fact]
    public void Create_ShouldCreateMapping_WhenValidValues()
    {
        // Act
        var mapping = GroupMapping.Create(MappingType.Claim, "department", "IT", TokenTarget.Both);

        // Assert
        mapping.Type.ShouldBe(MappingType.Claim);
        mapping.Target.ShouldBe("department");
        mapping.Value.ShouldBe("IT");
        mapping.TokenTarget.ShouldBe(TokenTarget.Both);
    }

    [Fact]
    public void Create_ShouldThrow_WhenTargetIsEmpty()
    {
        // Act
        Func<GroupMapping> act = () => GroupMapping.Create(MappingType.Claim, "", "value", TokenTarget.AccessToken);

        // Assert
        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void Create_ShouldThrow_WhenRoleHasValue()
    {
        // Act
        Func<GroupMapping> act = () => GroupMapping.Create(MappingType.Role, "Admin", "SomeValue", TokenTarget.AccessToken);

        // Assert
        Should.Throw<ArgumentException>(act).Message.ShouldContain("Role mappings should not have a value");
    }

    [Fact]
    public void Create_ShouldAllowNullValue_ForClaim()
    {
        // Act
        var mapping = GroupMapping.Create(MappingType.Claim, "premium", null, TokenTarget.AccessToken);

        // Assert
        mapping.Value.ShouldBeNull();
    }

    [Fact]
    public void Equality_ShouldWorkCorrectly()
    {
        // Arrange
        var m1 = GroupMapping.Create(MappingType.Role, "Admin", null, TokenTarget.AccessToken);
        var m2 = GroupMapping.Create(MappingType.Role, "Admin", null, TokenTarget.AccessToken);
        var m3 = GroupMapping.Create(MappingType.Role, "User", null, TokenTarget.AccessToken);

        // Assert
        m1.ShouldBe(m2);
        m1.ShouldNotBe(m3);
        (m1 == m2).ShouldBeTrue();
    }
}
