using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Groups;

public class GroupMappingTests
{
    [Theory]
    [InlineData("permission")]
    [InlineData("permissions")]
    [InlineData("scope")]
    [InlineData("sub")]
    [InlineData("role")]
    [InlineData("auth_time")]
    [InlineData("nonce")]
    [InlineData("requested_userinfo_claim")]
    [InlineData("ois_human_subject")]
    [InlineData("ois.example")]
    [InlineData("OIS.example")]
    [InlineData("ois_human_authenticated_at")]
    [InlineData("oi_prst")]
    [InlineData("phone_number_verified")]
    [InlineData("phone_number")]
    [InlineData(" PHONE_NUMBER ")]
    [InlineData("name")]
    [InlineData("given_name")]
    [InlineData("family_name")]
    [InlineData("middle_name")]
    [InlineData("nickname")]
    [InlineData("preferred_username")]
    [InlineData("profile")]
    [InlineData("picture")]
    [InlineData("website")]
    [InlineData("gender")]
    [InlineData("birthdate")]
    [InlineData("zoneinfo")]
    [InlineData("locale")]
    [InlineData("address")]
    [InlineData("updated_at")]
    [InlineData(" OIS_human_authenticated_at ")]
    [InlineData(" oi_prst ")]
    [InlineData(" ois.example ")]
    public void SecurityClaimsCannotBeSuppliedByGroupMappings(string type)
    {
        GroupMapping.Create(MappingType.Claim, type, "*", TokenTarget.Both).IsFailure.ShouldBeTrue();
    }

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
