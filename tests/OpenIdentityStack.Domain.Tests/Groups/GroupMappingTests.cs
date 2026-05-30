using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Groups;

public class GroupMappingTests
{
    [Fact]
    public void Create_ShouldCreateMapping_WhenValidValues()
    {
        // Act
        GroupMapping mapping = GroupMapping.Create(MappingType.Claim, "department", "IT", TokenTarget.Both).Value;

        // Assert
        mapping.Type.ShouldBe(MappingType.Claim);
        mapping.Target.ShouldBe("department");
        mapping.Value.ShouldBe("IT");
        mapping.TokenTarget.ShouldBe(TokenTarget.Both);
    }

    [Fact]
    public void Create_ShouldFail_WhenTargetIsEmpty()
    {
        // Act
        Result<GroupMapping> result = GroupMapping.Create(MappingType.Claim, "", "value", TokenTarget.AccessToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GroupMappingErrors.TargetRequired);
    }

    [Fact]
    public void Create_ShouldFail_WhenRoleHasValue()
    {
        // Act
        Result<GroupMapping> result = GroupMapping.Create(MappingType.Role, "Admin", "SomeValue", TokenTarget.AccessToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GroupMappingErrors.RoleMappingValueNotAllowed);
    }

    [Fact]
    public void Create_ShouldAllowNullValue_ForClaim()
    {
        // Act
        GroupMapping mapping = GroupMapping.Create(MappingType.Claim, "premium", null, TokenTarget.AccessToken).Value;

        // Assert
        mapping.Value.ShouldBeNull();
    }

    [Fact]
    public void Equality_ShouldWorkCorrectly()
    {
        // Arrange
        GroupMapping m1 = GroupMapping.Create(MappingType.Role, "Admin", null, TokenTarget.AccessToken).Value;
        GroupMapping m2 = GroupMapping.Create(MappingType.Role, "Admin", null, TokenTarget.AccessToken).Value;
        GroupMapping m3 = GroupMapping.Create(MappingType.Role, "User", null, TokenTarget.AccessToken).Value;

        // Assert
        m1.ShouldBe(m2);
        m1.ShouldNotBe(m3);
        (m1 == m2).ShouldBeTrue();
    }
}
