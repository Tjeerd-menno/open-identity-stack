using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Groups;

public class GroupTests
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public GroupTests()
    {
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Create_ShouldCreateGroup_WhenValidInputs()
    {
        // Arrange
        string name = "Developers";
        string description = "Software Developers";

        // Act
        Result<Group> result = Group.Create(name, description, this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.Value.ShouldNotBe(Guid.Empty);
        result.Value.Name.ShouldBe(name);
        result.Value.Description.ShouldBe(description);
        result.Value.CreatedAt.ShouldBe(this._dateTimeProvider.UtcNow);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldFail_WhenNameIsEmpty(string? invalidName)
    {
        // Act
        // We know string.IsNullOrWhiteSpace covers null, but C# compiler might complain about passing null to non-nullable if we call Create directly if definition assumes non-null.
        // Group.Create signature is (string name, ...).
        // If I pass null to it, it might warn if NRT enabled.
        // But for test purposes, I'll pass it as string! (using null forgiving) or change helper variable.
        string name = invalidName ?? string.Empty;
        // Wait, if I pass string.Empty it fails for empty.
        // If I pass null, it's captured by IsNullOrWhiteSpace?
        // Let's just suppress the warning or allow nullable.

        Result<Group> result = Group.Create(invalidName!, "desc", this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GroupErrors.NameRequired);
    }

    [Fact]
    public void Create_ShouldFail_WhenNameTooLong()
    {
        // Arrange
        string name = new string('a', 129);

        // Act
        Result<Group> result = Group.Create(name, "desc", this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GroupErrors.NameTooLong);
    }

    [Fact]
    public void Create_ShouldFail_WhenDescriptionTooLong()
    {
        // Arrange
        string description = new string('b', 513);

        // Act
        Result<Group> result = Group.Create("Group", description, this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GroupErrors.DescriptionTooLong);
    }

    [Fact]
    public void Update_ShouldUpdateProperties_WhenValid()
    {
        // Arrange
        Group group = Group.Create("OldName", "OldDesc", this._dateTimeProvider).Value;
        string newName = "NewName";
        string newDesc = "NewDesc";
        DateTimeOffset updateTime = this._dateTimeProvider.UtcNow.AddHours(1);
        this._dateTimeProvider.UtcNow.Returns(updateTime);

        // Act
        Result result = group.Update(newName, newDesc, this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        group.Name.ShouldBe(newName);
        group.Description.ShouldBe(newDesc);
        group.ModifiedAt.ShouldBe(updateTime);
    }

    [Fact]
    public void Update_ShouldFail_WhenNameIsEmpty()
    {
        // Arrange
        Group group = Group.Create("Name", "Desc", this._dateTimeProvider).Value;

        // Act
        Result result = group.Update("", "Desc", this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GroupErrors.NameRequired);
    }

    [Fact]
    public void Update_ShouldFail_WhenNameTooLong()
    {
        // Arrange
        Group group = Group.Create("Name", "Desc", this._dateTimeProvider).Value;
        string name = new string('a', 129);

        // Act
        Result result = group.Update(name, "Desc", this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GroupErrors.NameTooLong);
    }

    [Fact]
    public void Update_ShouldFail_WhenDescriptionTooLong()
    {
        // Arrange
        Group group = Group.Create("Name", "Desc", this._dateTimeProvider).Value;
        string description = new string('b', 513);

        // Act
        Result result = group.Update("Name", description, this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GroupErrors.DescriptionTooLong);
    }

    [Fact]
    public void SetParent_ShouldFail_WhenCircular()
    {
        // Arrange
        Group group = Group.Create("Group", "Desc", this._dateTimeProvider).Value;

        // Act
        Result result = group.SetParent(group, this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GroupErrors.CircularReference);
    }

    [Fact]
    public void AddMember_ShouldAddUser_WhenNotExists()
    {
        // Arrange
        Group group = Group.Create("Group", "Desc", this._dateTimeProvider).Value;
        var userId = UserId.Create();
        var assignedBy = UserId.Create();

        // Act
        group.AddMember(userId, assignedBy, this._dateTimeProvider);

        // Assert
        group.Memberships.Count.ShouldBe(1);
        group.Memberships.ShouldContain(m => m.UserId == userId && m.AssignedBy == assignedBy);
    }

    [Fact]
    public void AddMember_ShouldIgnoreEmptyUserId()
    {
        // Arrange
        Group group = Group.Create("Group", "Desc", this._dateTimeProvider).Value;
        var assignedBy = UserId.Create();

        // Act
        group.AddMember(UserId.Empty, assignedBy, this._dateTimeProvider);

        // Assert
        group.Memberships.ShouldBeEmpty();
    }

    [Fact]
    public void AddMember_ShouldNotAddDuplicate_WhenExists()
    {
        // Arrange
        Group group = Group.Create("Group", "Desc", this._dateTimeProvider).Value;
        var userId = UserId.Create();
        var assignedBy = UserId.Create();
        group.AddMember(userId, assignedBy, this._dateTimeProvider);

        // Act
        group.AddMember(userId, assignedBy, this._dateTimeProvider);

        // Assert
        group.Memberships.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveMember_ShouldRemoveUser_WhenExists()
    {
        // Arrange
        Group group = Group.Create("Group", "Desc", this._dateTimeProvider).Value;
        var userId = UserId.Create();
        group.AddMember(userId, UserId.Create(), this._dateTimeProvider);

        // Act
        group.RemoveMember(userId, this._dateTimeProvider);

        // Assert
        group.Memberships.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveMember_ShouldDoNothing_WhenMissing()
    {
        // Arrange
        Group group = Group.Create("Group", "Desc", this._dateTimeProvider).Value;

        // Act
        group.RemoveMember(UserId.Create(), this._dateTimeProvider);

        // Assert
        group.Memberships.ShouldBeEmpty();
    }

    [Fact]
    public void AddMapping_ShouldAddMapping_WhenNew()
    {
        // Arrange
        Group group = Group.Create("Group", "Desc", this._dateTimeProvider).Value;

        // Act
        group.AddMapping(MappingType.Role, "Admin", null, TokenTarget.AccessToken, this._dateTimeProvider);

        // Assert
        group.Mappings.Count.ShouldBe(1);
        group.Mappings.First().Target.ShouldBe("Admin");
        group.Mappings.First().Type.ShouldBe(MappingType.Role);
    }

    [Fact]
    public void AddMapping_ShouldNotAddDuplicate()
    {
        // Arrange
        Group group = Group.Create("Group", "Desc", this._dateTimeProvider).Value;
        group.AddMapping(MappingType.Role, "Admin", null, TokenTarget.AccessToken, this._dateTimeProvider);

        // Act
        group.AddMapping(MappingType.Role, "Admin", null, TokenTarget.AccessToken, this._dateTimeProvider);

        // Assert
        group.Mappings.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveMapping_ShouldRemove_WhenExists()
    {
        // Arrange
        Group group = Group.Create("Group", "Desc", this._dateTimeProvider).Value;
        group.AddMapping(MappingType.Role, "Admin", null, TokenTarget.AccessToken, this._dateTimeProvider);
        GroupMapping mapping = group.Mappings.First();

        // Act
        group.RemoveMapping(mapping, this._dateTimeProvider);

        // Assert
        group.Mappings.ShouldBeEmpty();
    }
}
