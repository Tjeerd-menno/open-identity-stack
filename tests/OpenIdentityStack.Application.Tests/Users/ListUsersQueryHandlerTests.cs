using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Users;

/// <summary>
/// Unit tests for ListUsersQueryHandler.
/// </summary>
public sealed class ListUsersQueryHandlerTests
{
    private readonly IUserRepository _userRepository;
    private readonly ListUsersQueryHandler _handler;
    private readonly IDateTimeProvider _dateTimeProvider;
    private static readonly DateTimeOffset TestTime = new(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);

    public ListUsersQueryHandlerTests()
    {
        this._userRepository = Substitute.For<IUserRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(TestTime);
        this._handler = new ListUsersQueryHandler(this._userRepository);
    }

    [Fact]
    public async Task HandleAsync_NoUsers_ReturnsEmptyList()
    {
        // Arrange
        var query = new ListUsersQuery(Page: 1, PageSize: 20);

        this._userRepository.ListAsync(1, 20, null, Arg.Any<CancellationToken>())
            .Returns((new List<User>(), 0));

        // Act
        Result<ListUsersResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
        result.Value.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_WithUsers_ReturnsAllItems()
    {
        // Arrange
        var query = new ListUsersQuery(Page: 1, PageSize: 20);
        User user1 = CreateUser("alice@example.com", "Alice");
        User user2 = CreateUser("bob@example.com", "Bob");

        this._userRepository.ListAsync(1, 20, null, Arg.Any<CancellationToken>())
            .Returns((new List<User> { user1, user2 }, 2));

        // Act
        Result<ListUsersResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task HandleAsync_WithUsers_MapsEmailCorrectly()
    {
        // Arrange
        var query = new ListUsersQuery(Page: 1, PageSize: 20);
        User user = CreateUser("alice@example.com", "Alice");

        this._userRepository.ListAsync(1, 20, null, Arg.Any<CancellationToken>())
            .Returns((new List<User> { user }, 1));

        // Act
        Result<ListUsersResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.Value.Items[0].Email.ShouldBe("alice@example.com");
    }

    [Fact]
    public async Task HandleAsync_WithUsers_MapsDisplayNameCorrectly()
    {
        // Arrange
        var query = new ListUsersQuery(Page: 1, PageSize: 20);
        User user = CreateUser("alice@example.com", "Alice");

        this._userRepository.ListAsync(1, 20, null, Arg.Any<CancellationToken>())
            .Returns((new List<User> { user }, 1));

        // Act
        Result<ListUsersResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.Value.Items[0].DisplayName.ShouldBe("Alice");
    }

    [Fact]
    public async Task HandleAsync_ReturnsPaginationMetadata()
    {
        // Arrange
        var query = new ListUsersQuery(Page: 2, PageSize: 10);
        User user = CreateUser();

        this._userRepository.ListAsync(2, 10, null, Arg.Any<CancellationToken>())
            .Returns((new List<User> { user }, 25));

        // Act
        Result<ListUsersResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.Value.Page.ShouldBe(2);
        result.Value.PageSize.ShouldBe(10);
        result.Value.TotalCount.ShouldBe(25);
        result.Value.TotalPages.ShouldBe(3);
    }

    [Fact]
    public async Task HandleAsync_PageLessThanOne_NormalizesToOne()
    {
        // Arrange
        var query = new ListUsersQuery(Page: 0, PageSize: 10);

        this._userRepository.ListAsync(1, 10, null, Arg.Any<CancellationToken>())
            .Returns((new List<User>(), 0));

        // Act
        Result<ListUsersResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Page.ShouldBe(1);
        await this._userRepository.Received(1).ListAsync(1, 10, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PageSizeExceedsMaximum_ClampedTo100()
    {
        // Arrange
        var query = new ListUsersQuery(Page: 1, PageSize: 200);

        this._userRepository.ListAsync(1, 100, null, Arg.Any<CancellationToken>())
            .Returns((new List<User>(), 0));

        // Act
        Result<ListUsersResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.PageSize.ShouldBe(100);
        await this._userRepository.Received(1).ListAsync(1, 100, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PageSizeLessThanOne_ClampedToOne()
    {
        // Arrange
        var query = new ListUsersQuery(Page: 1, PageSize: 0);

        this._userRepository.ListAsync(1, 1, null, Arg.Any<CancellationToken>())
            .Returns((new List<User>(), 0));

        // Act
        Result<ListUsersResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.PageSize.ShouldBe(1);
        await this._userRepository.Received(1).ListAsync(1, 1, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithSearchTerm_ForwardsSearchToRepository()
    {
        // Arrange
        var query = new ListUsersQuery(Page: 1, PageSize: 20, Search: "alice");

        this._userRepository.ListAsync(1, 20, "alice", Arg.Any<CancellationToken>())
            .Returns((new List<User>(), 0));

        // Act
        Result<ListUsersResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await this._userRepository.Received(1).ListAsync(1, 20, "alice", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_HasNextPage_ReturnsTrueWhenMorePages()
    {
        // Arrange
        var query = new ListUsersQuery(Page: 1, PageSize: 10);

        this._userRepository.ListAsync(1, 10, null, Arg.Any<CancellationToken>())
            .Returns((new List<User>(), 25));

        // Act
        Result<ListUsersResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.Value.HasNextPage.ShouldBeTrue();
        result.Value.HasPreviousPage.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_HasPreviousPage_ReturnsTrueOnLaterPages()
    {
        // Arrange
        var query = new ListUsersQuery(Page: 2, PageSize: 10);

        this._userRepository.ListAsync(2, 10, null, Arg.Any<CancellationToken>())
            .Returns((new List<User>(), 25));

        // Act
        Result<ListUsersResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.Value.HasPreviousPage.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_PassesCancellationToken()
    {
        // Arrange
        var query = new ListUsersQuery();
        using var cts = new CancellationTokenSource();

        this._userRepository.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), cts.Token)
            .Returns((new List<User>(), 0));

        // Act
        await this._handler.HandleAsync(query, cts.Token);

        // Assert
        await this._userRepository.Received(1).ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), cts.Token);
    }

    private User CreateUser(string email = "user@example.com", string displayName = "Test User")
    {
        Result<User> result = User.CreateLocal(email, displayName, "hashedPassword", this._dateTimeProvider);
        return result.Value;
    }
}
