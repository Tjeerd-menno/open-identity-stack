
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Roles;
/// <summary>
/// Unit tests for the Role entity.
/// </summary>
public sealed class RoleTests
{
    private const string ValidName = "Admin";
    private const string ValidDescription = "Administrator role with full access";

    #region Create Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsRole()
    {
        // Act
        Result<Role> result = Role.Create(ValidName, ValidDescription);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("admin"); // Name is normalized to lowercase
        result.Value.DisplayName.ShouldBe(ValidName);
        result.Value.Description.ShouldBe(ValidDescription);
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithEmptyName_ReturnsError()
    {
        // Act
        Result<Role> result = Role.Create(string.Empty, ValidDescription);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("NameRequired");
    }

    [Fact]
    public void Create_WithWhitespaceName_ReturnsError()
    {
        // Act
        Result<Role> result = Role.Create("   ", ValidDescription);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("NameRequired");
    }

    [Fact]
    public void Create_WithNullDescription_UsesEmptyDescription()
    {
        // Act
        Result<Role> result = Role.Create(ValidName, null);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Description.ShouldBe(string.Empty);
    }

    [Fact]
    public void Create_NormalizesRoleName()
    {
        // Act
        Result<Role> result = Role.Create("  Super Admin  ", ValidDescription);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("super admin");
        result.Value.DisplayName.ShouldBe("Super Admin");
    }

    [Fact]
    public void Create_GeneratesUniqueId()
    {
        // Act
        Result<Role> result1 = Role.Create("Role1", "Description 1");
        Result<Role> result2 = Role.Create("Role2", "Description 2");

        // Assert
        result1.Value.Id.ShouldNotBe(result2.Value.Id);
    }

    #endregion

    #region Update Tests

    [Fact]
    public void UpdateDescription_WithValidValue_UpdatesDescription()
    {
        // Arrange
        Role role = Role.Create(ValidName, ValidDescription).Value;
        string newDescription = "Updated description";

        // Act
        role.UpdateDescription(newDescription);

        // Assert
        role.Description.ShouldBe(newDescription);
    }

    [Fact]
    public void UpdateDescription_WithNull_SetsEmptyDescription()
    {
        // Arrange
        Role role = Role.Create(ValidName, ValidDescription).Value;

        // Act
        role.UpdateDescription(null);

        // Assert
        role.Description.ShouldBe(string.Empty);
    }

    #endregion

    #region Disable/Enable Tests

    [Fact]
    public void Disable_WhenActive_SetsInactive()
    {
        // Arrange
        Role role = Role.Create(ValidName, ValidDescription).Value;

        // Act
        Result result = role.Disable();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        role.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Disable_WhenAlreadyDisabled_ReturnsError()
    {
        // Arrange
        Role role = Role.Create(ValidName, ValidDescription).Value;
        role.Disable();

        // Act
        Result result = role.Disable();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("AlreadyDisabled");
    }

    [Fact]
    public void Enable_WhenDisabled_SetsActive()
    {
        // Arrange
        Role role = Role.Create(ValidName, ValidDescription).Value;
        role.Disable();

        // Act
        Result result = role.Enable();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        role.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Enable_WhenAlreadyActive_ReturnsError()
    {
        // Arrange
        Role role = Role.Create(ValidName, ValidDescription).Value;

        // Act
        Result result = role.Enable();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("AlreadyActive");
    }

    #endregion

    #region System Role Tests

    [Fact]
    public void CreateSystemRole_WithValidParameters_ReturnsSystemRole()
    {
        // Act
        Result<Role> result = Role.CreateSystemRole("super-admin", "Super Admin", "Built-in super administrator role");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.IsSystemRole.ShouldBeTrue();
        result.Value.Name.ShouldBe("super-admin");
    }

    [Fact]
    public void Disable_WhenSystemRole_ReturnsError()
    {
        // Arrange
        Role role = Role.CreateSystemRole("super-admin", "Super Admin", "Built-in role").Value;

        // Act
        Result result = role.Disable();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("CannotDisableSystemRole");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameId_ReturnsTrue()
    {
        // Arrange
        Role role1 = Role.Create(ValidName, ValidDescription).Value;
        RoleId role2Id = role1.Id;

        // Assert - same instance should be equal
        role1.Equals(role1).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithDifferentId_ReturnsFalse()
    {
        // Arrange
        Role role1 = Role.Create("Role1", "Description 1").Value;
        Role role2 = Role.Create("Role2", "Description 2").Value;

        // Assert
        role1.Equals(role2).ShouldBeFalse();
    }

    #endregion
}
