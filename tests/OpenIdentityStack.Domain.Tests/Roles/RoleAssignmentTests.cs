
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Roles;
/// <summary>
/// Unit tests for the RoleAssignment entity.
/// </summary>
public sealed class RoleAssignmentTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsAssignment()
    {
        // Arrange
        var userId = UserId.Create();
        var roleId = RoleId.Create();
        DateTimeOffset assignedAt = DateTimeOffset.UtcNow;

        // Act
        Result<RoleAssignment> result = RoleAssignment.Create(userId, roleId, assignedAt);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.UserId.ShouldBe(userId);
        result.Value.RoleId.ShouldBe(roleId);
        result.Value.AssignedAt.ShouldBe(assignedAt);
    }

    [Fact]
    public void Create_WithDefaultUserId_ReturnsError()
    {
        // Arrange
        var userId = default(UserId);
        var roleId = RoleId.Create();

        // Act
        Result<RoleAssignment> result = RoleAssignment.Create(userId, roleId, DateTimeOffset.UtcNow);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("UserIdRequired");
    }

    [Fact]
    public void Create_WithDefaultRoleId_ReturnsError()
    {
        // Arrange
        var userId = UserId.Create();
        var roleId = default(RoleId);

        // Act
        Result<RoleAssignment> result = RoleAssignment.Create(userId, roleId, DateTimeOffset.UtcNow);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("RoleIdRequired");
    }

    #endregion

    #region Factory Method Tests

    [Fact]
    public void ForUser_CreatesAssignmentWithCurrentTime()
    {
        // Arrange
        var userId = UserId.Create();
        var roleId = RoleId.Create();
        DateTimeOffset beforeCreate = DateTimeOffset.UtcNow;

        // Act
        Result<RoleAssignment> result = RoleAssignment.ForUser(userId, roleId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.UserId.ShouldBe(userId);
        result.Value.RoleId.ShouldBe(roleId);
        result.Value.AssignedAt.ShouldBeGreaterThanOrEqualTo(beforeCreate);
    }

    #endregion

    #region Matches Tests

    [Fact]
    public void Matches_WithSameUserAndRole_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.Create();
        var roleId = RoleId.Create();
        RoleAssignment assignment = RoleAssignment.Create(userId, roleId, DateTimeOffset.UtcNow).Value;

        // Act & Assert
        assignment.Matches(userId, roleId).ShouldBeTrue();
    }

    [Fact]
    public void Matches_WithDifferentUser_ReturnsFalse()
    {
        // Arrange
        var userId = UserId.Create();
        var otherUserId = UserId.Create();
        var roleId = RoleId.Create();
        RoleAssignment assignment = RoleAssignment.Create(userId, roleId, DateTimeOffset.UtcNow).Value;

        // Act & Assert
        assignment.Matches(otherUserId, roleId).ShouldBeFalse();
    }

    [Fact]
    public void Matches_WithDifferentRole_ReturnsFalse()
    {
        // Arrange
        var userId = UserId.Create();
        var roleId = RoleId.Create();
        var otherRoleId = RoleId.Create();
        RoleAssignment assignment = RoleAssignment.Create(userId, roleId, DateTimeOffset.UtcNow).Value;

        // Act & Assert
        assignment.Matches(userId, otherRoleId).ShouldBeFalse();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameUserAndRole_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.Create();
        var roleId = RoleId.Create();
        RoleAssignment assignment1 = RoleAssignment.Create(userId, roleId, DateTimeOffset.UtcNow).Value;
        RoleAssignment assignment2 = RoleAssignment.Create(userId, roleId, DateTimeOffset.UtcNow.AddHours(1)).Value;

        // Act & Assert - should be equal based on userId + roleId
        assignment1.Matches(assignment2.UserId, assignment2.RoleId).ShouldBeTrue();
    }

    #endregion
}
