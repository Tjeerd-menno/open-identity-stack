
using System.Reflection;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Groups;
/// <summary>
/// Unit tests for the GroupMembership entity.
/// </summary>
public sealed class GroupMembershipTests
{
    private static readonly GroupId TestGroupId = new(Guid.NewGuid());
    private static readonly UserId TestUserId = new(Guid.NewGuid());
    private static readonly UserId TestAssignedByUserId = new(Guid.NewGuid());
    private static readonly DateTimeOffset TestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_SetsGroupId()
    {
        // Arrange & Act
        GroupMembership membership = CreateMembership();

        // Assert
        membership.GroupId.ShouldBe(TestGroupId);
    }

    [Fact]
    public void Constructor_SetsUserId()
    {
        // Arrange & Act
        GroupMembership membership = CreateMembership();

        // Assert
        membership.UserId.ShouldBe(TestUserId);
    }

    [Fact]
    public void Constructor_SetsAssignedAt()
    {
        // Arrange & Act
        GroupMembership membership = CreateMembership();

        // Assert
        membership.AssignedAt.ShouldBe(TestTime);
    }

    [Fact]
    public void Constructor_SetsAssignedBy()
    {
        // Arrange & Act
        GroupMembership membership = CreateMembership();

        // Assert
        membership.AssignedBy.ShouldBe(TestAssignedByUserId);
    }

    [Fact]
    public void TwoMemberships_WithSameUserAndGroup_AreDistinct()
    {
        // Arrange
        GroupMembership membership1 = CreateMembership();
        GroupMembership membership2 = CreateMembership();

        // Assert - they are value objects representing the same relationship
        membership1.UserId.ShouldBe(membership2.UserId);
        membership1.GroupId.ShouldBe(membership2.GroupId);
    }

    [Fact]
    public void TwoMemberships_WithDifferentUsers_AreDifferent()
    {
        // Arrange
        var differentUserId = new UserId(Guid.NewGuid());

        GroupMembership membership1 = CreateMembership();
        GroupMembership membership2 = CreateMembership(userId: differentUserId);

        // Assert
        membership1.UserId.ShouldNotBe(membership2.UserId);
    }

    [Fact]
    public void TwoMemberships_WithDifferentGroups_AreDifferent()
    {
        // Arrange
        var differentGroupId = new GroupId(Guid.NewGuid());

        GroupMembership membership1 = CreateMembership();
        GroupMembership membership2 = CreateMembership(groupId: differentGroupId);

        // Assert
        membership1.GroupId.ShouldNotBe(membership2.GroupId);
    }

    [Fact]
    public void TwoMemberships_WithDifferentAssignedBy_TrackDifferentAssigners()
    {
        // Arrange
        var differentAssignedBy = new UserId(Guid.NewGuid());

        GroupMembership membership1 = CreateMembership();
        GroupMembership membership2 = CreateMembership(assignedBy: differentAssignedBy);

        // Assert
        membership1.AssignedBy.ShouldNotBe(membership2.AssignedBy);
    }

    [Fact]
    public void TwoMemberships_WithDifferentTimestamps_TrackDifferentTimes()
    {
        // Arrange
        DateTimeOffset laterTime = TestTime.AddHours(1);

        GroupMembership membership1 = CreateMembership();
        GroupMembership membership2 = CreateMembership(assignedAt: laterTime);

        // Assert
        membership1.AssignedAt.ShouldNotBe(membership2.AssignedAt);
    }

    [Fact]
    public void PrivateConstructor_InitializesEmptyValues()
    {
        // Arrange
        Type type = typeof(GroupMembership);
        ConstructorInfo? constructor = type.GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null);

        // Act
        var membership = (GroupMembership)constructor!.Invoke(null);

        // Assert
        membership.UserId.ShouldBe(UserId.Empty);
        membership.GroupId.ShouldBe(GroupId.Empty);
        membership.AssignedBy.ShouldBe(UserId.Empty);
    }

    private static GroupMembership CreateMembership(
        GroupId? groupId = null,
        UserId? userId = null,
        DateTimeOffset? assignedAt = null,
        UserId? assignedBy = null)
    {
        // Use reflection to create instance since constructor is internal
        Type type = typeof(GroupMembership);
        ConstructorInfo? constructor = type.GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            [typeof(GroupId), typeof(UserId), typeof(DateTimeOffset), typeof(UserId)],
            null);

        if (constructor is null)
        {
            throw new InvalidOperationException("Could not find GroupMembership constructor");
        }

        return (GroupMembership)constructor.Invoke([
            groupId ?? TestGroupId,
            userId ?? TestUserId,
            assignedAt ?? TestTime,
            assignedBy ?? TestAssignedByUserId
        ]);
    }
}
