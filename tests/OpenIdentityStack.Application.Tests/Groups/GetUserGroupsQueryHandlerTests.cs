using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Groups;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Groups;
/// <summary>
/// Unit tests for GetUserGroupsQueryHandler.
/// </summary>
public sealed class GetUserGroupsQueryHandlerTests
{
    private readonly IGroupRepository _groupRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly GetUserGroupsQueryHandler _handler;
    private static readonly DateTimeOffset TestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

    public GetUserGroupsQueryHandlerTests()
    {
        this._groupRepository = Substitute.For<IGroupRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(TestTime);
        this._handler = new GetUserGroupsQueryHandler(this._groupRepository);
    }

    private Group CreateGroup(string name, string? description = null)
    {
        return Group.Create(name, description, this._dateTimeProvider).Value;
    }

    [Fact]
    public async Task HandleAsync_UserWithNoGroups_ReturnsEmptyList()
    {
        // Arrange
        var userId = UserId.Create();
        this._groupRepository
            .GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        Result<List<GroupResponse>> result = await this._handler.HandleAsync(userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_UserWithSingleGroup_ReturnsGroupResponse()
    {
        // Arrange
        var userId = UserId.Create();
        Group group = this.CreateGroup("developers", "Developers Team");

        this._groupRepository
            .GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns([group]);

        // Act
        Result<List<GroupResponse>> result = await this._handler.HandleAsync(userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);
        result.Value[0].Name.ShouldBe("developers");
        result.Value[0].Description.ShouldBe("Developers Team");
    }

    [Fact]
    public async Task HandleAsync_UserWithMultipleGroups_ReturnsAllGroups()
    {
        // Arrange
        var userId = UserId.Create();

        Group group1 = this.CreateGroup("admins", "Administrators");
        Group group2 = this.CreateGroup("users", "Regular Users");
        Group group3 = this.CreateGroup("managers", "Managers");

        this._groupRepository
            .GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns([group1, group2, group3]);

        // Act
        Result<List<GroupResponse>> result = await this._handler.HandleAsync(userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(3);
        result.Value.ShouldContain(g => g.Name == "admins");
        result.Value.ShouldContain(g => g.Name == "users");
        result.Value.ShouldContain(g => g.Name == "managers");
    }

    [Fact]
    public async Task HandleAsync_ReturnsGroupId()
    {
        // Arrange
        var userId = UserId.Create();
        Group group = this.CreateGroup("test-group", "Test");

        this._groupRepository
            .GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns([group]);

        // Act
        Result<List<GroupResponse>> result = await this._handler.HandleAsync(userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value[0].Id.ShouldBe(group.Id.Value);
    }

    [Fact]
    public async Task HandleAsync_GroupWithParent_ReturnsParentGroupId()
    {
        // Arrange
        var userId = UserId.Create();
        Group parentGroup = this.CreateGroup("parent", "Parent Group");
        Group childGroup = this.CreateGroup("child", "Child Group");
        childGroup.SetParent(parentGroup, this._dateTimeProvider);

        this._groupRepository
            .GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns([childGroup]);

        // Act
        Result<List<GroupResponse>> result = await this._handler.HandleAsync(userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value[0].ParentGroupId.ShouldBe(parentGroup.Id.Value);
    }

    [Fact]
    public async Task HandleAsync_GroupWithoutParent_ReturnsNullParentId()
    {
        // Arrange
        var userId = UserId.Create();
        Group group = this.CreateGroup("standalone", "Standalone Group");

        this._groupRepository
            .GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns([group]);

        // Act
        Result<List<GroupResponse>> result = await this._handler.HandleAsync(userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value[0].ParentGroupId.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_ReturnsCreatedAtTimestamp()
    {
        // Arrange
        var userId = UserId.Create();
        Group group = this.CreateGroup("dated-group", "Dated");

        this._groupRepository
            .GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns([group]);

        // Act
        Result<List<GroupResponse>> result = await this._handler.HandleAsync(userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value[0].CreatedAt.ShouldBe(group.CreatedAt.UtcDateTime);
    }

    [Fact]
    public async Task HandleAsync_PropagatesCancellation()
    {
        // Arrange
        var userId = UserId.Create();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        this._groupRepository
            .GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return Task.FromResult<IReadOnlyList<Group>>([]);
            });

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => this._handler.HandleAsync(userId, cts.Token));
    }
}
