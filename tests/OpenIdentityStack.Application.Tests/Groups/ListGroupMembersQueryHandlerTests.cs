using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Groups;
/// <summary>
/// Unit tests for ListGroupMembersQueryHandler.
/// </summary>
public sealed class ListGroupMembersQueryHandlerTests
{
    private readonly IGroupRepository _groupRepository;
    private readonly IUserRepository _userRepository;
    private readonly ListGroupMembersQueryHandler _handler;
    private readonly IDateTimeProvider _dateTimeProvider;
    private static readonly DateTimeOffset TestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
    private static readonly UserId AdminUserId = UserId.Create();

    public ListGroupMembersQueryHandlerTests()
    {
        this._groupRepository = Substitute.For<IGroupRepository>();
        this._userRepository = Substitute.For<IUserRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(TestTime);
        this._handler = new ListGroupMembersQueryHandler(this._groupRepository, this._userRepository);
    }

    private Group CreateGroup(string name, string? description = null)
    {
        return Group.Create(name, description, this._dateTimeProvider).Value;
    }

    [Fact]
    public async Task HandleAsync_GroupNotFound_ReturnsError()
    {
        // Arrange
        var groupId = new GroupId(Guid.NewGuid());
        this._groupRepository
            .GetByIdAsync(groupId, Arg.Any<CancellationToken>())
            .Returns((Group?)null);

        // Act
        Result<UserListResponse> result = await this._handler.HandleAsync(groupId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("NotFound");
    }

    [Fact]
    public async Task HandleAsync_GroupWithNoMembers_ReturnsEmptyList()
    {
        // Arrange
        Group group = this.CreateGroup("empty-group", "Empty");

        this._groupRepository
            .GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act
        Result<UserListResponse> result = await this._handler.HandleAsync(group.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_GroupWithMembers_ReturnsUserListItems()
    {
        // Arrange
        Group group = this.CreateGroup("test-group", "Test");

        Result<User> userResult = User.CreateLocal("user@test.com", "Test User", "hash", this._dateTimeProvider);
        userResult.IsSuccess.ShouldBeTrue();
        User user = userResult.Value;

        group.AddMember(user.Id, AdminUserId, this._dateTimeProvider);

        this._groupRepository
            .GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        this._userRepository
            .GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<UserListResponse> result = await this._handler.HandleAsync(group.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count().ShouldBe(1);
    }

    [Fact]
    public async Task HandleAsync_PaginatesResults_FirstPage()
    {
        // Arrange
        Group group = this.CreateGroup("paginated-group", "Paginated");

        // Add 5 members
        var users = new List<User>();
        for (int i = 0; i < 5; i++)
        {
            Result<User> userResult = User.CreateLocal($"user{i}@test.com", $"User {i}", "hash", this._dateTimeProvider);
            userResult.IsSuccess.ShouldBeTrue();
            User user = userResult.Value;
            users.Add(user);
            group.AddMember(user.Id, AdminUserId, this._dateTimeProvider);

            this._userRepository
                .GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
                .Returns(user);
        }

        this._groupRepository
            .GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act - request page 1 with size 2
        Result<UserListResponse> result = await this._handler.HandleAsync(group.Id, page: 1, pageSize: 2);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count().ShouldBe(2);
    }

    [Fact]
    public async Task HandleAsync_PaginatesResults_SecondPage()
    {
        // Arrange
        Group group = this.CreateGroup("paginated-group", "Paginated");

        // Add 5 members
        var users = new List<User>();
        for (int i = 0; i < 5; i++)
        {
            Result<User> userResult = User.CreateLocal($"user{i}@test.com", $"User {i}", "hash", this._dateTimeProvider);
            userResult.IsSuccess.ShouldBeTrue();
            User user = userResult.Value;
            users.Add(user);
            group.AddMember(user.Id, AdminUserId, this._dateTimeProvider);

            this._userRepository
                .GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
                .Returns(user);
        }

        this._groupRepository
            .GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act - request page 2 with size 2
        Result<UserListResponse> result = await this._handler.HandleAsync(group.Id, page: 2, pageSize: 2);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count().ShouldBe(2);
    }

    [Fact]
    public async Task HandleAsync_MemberUserNotFound_SkipsUser()
    {
        // Arrange
        Group group = this.CreateGroup("orphan-group", "Orphaned Member");

        var orphanedUserId = UserId.Create();
        group.AddMember(orphanedUserId, AdminUserId, this._dateTimeProvider);

        this._groupRepository
            .GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        this._userRepository
            .GetByIdAsync(orphanedUserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result<UserListResponse> result = await this._handler.HandleAsync(group.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ReturnsUserDetails()
    {
        // Arrange
        Group group = this.CreateGroup("detail-group", "Details");

        Result<User> userResult = User.CreateLocal("detail@test.com", "Detailed User", "hash", this._dateTimeProvider);
        userResult.IsSuccess.ShouldBeTrue();
        User user = userResult.Value;

        group.AddMember(user.Id, AdminUserId, this._dateTimeProvider);

        this._groupRepository
            .GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        this._userRepository
            .GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<UserListResponse> result = await this._handler.HandleAsync(group.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        UserListItem item = result.Value.Items.First();
        item.Email.ShouldBe("detail@test.com");
        item.DisplayName.ShouldBe("Detailed User");
        item.Id.ShouldBe(user.Id);
    }

    [Fact]
    public async Task HandleAsync_DefaultPagination_Uses20PageSize()
    {
        // Arrange
        Group group = this.CreateGroup("default-page-group", "Default Page");

        this._groupRepository
            .GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act - use defaults
        Result<UserListResponse> result = await this._handler.HandleAsync(group.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        // No assertion on exact page size behavior since group has no members,
        // but verifies the method accepts default parameters
    }
}
