using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Users;

/// <summary>
/// Unit tests for GetUserQueryHandler.
/// </summary>
public sealed class GetUserQueryHandlerTests
{
    private readonly IUserRepository _userRepository;
    private readonly GetUserQueryHandler _handler;
    private readonly IDateTimeProvider _dateTimeProvider;
    private static readonly DateTimeOffset TestTime = new(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);

    public GetUserQueryHandlerTests()
    {
        this._userRepository = Substitute.For<IUserRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(TestTime);
        this._handler = new GetUserQueryHandler(this._userRepository);
    }

    [Fact]
    public async Task HandleAsync_UserNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var userId = new UserId(Guid.NewGuid());
        var query = new GetUserQuery(userId);

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result<GetUserResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.User.NotFound");
    }

    [Fact]
    public async Task HandleAsync_UserFound_ReturnsSuccess()
    {
        // Arrange
        User user = CreateUser();
        var query = new GetUserQuery(user.Id);

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<GetUserResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_UserFound_MapsEmailCorrectly()
    {
        // Arrange
        User user = CreateUser("found@example.com", "Found User");
        var query = new GetUserQuery(user.Id);

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<GetUserResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.Value.Email.ShouldBe("found@example.com");
    }

    [Fact]
    public async Task HandleAsync_UserFound_MapsDisplayNameCorrectly()
    {
        // Arrange
        User user = CreateUser("found@example.com", "Found User");
        var query = new GetUserQuery(user.Id);

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<GetUserResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.Value.DisplayName.ShouldBe("Found User");
    }

    [Fact]
    public async Task HandleAsync_UserFound_MapsIdCorrectly()
    {
        // Arrange
        User user = CreateUser();
        var query = new GetUserQuery(user.Id);

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<GetUserResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.Value.Id.ShouldBe(user.Id);
    }

    [Fact]
    public async Task HandleAsync_UserFound_MapsStatusCorrectly()
    {
        // Arrange
        User user = CreateUser();
        var query = new GetUserQuery(user.Id);

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<GetUserResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.Value.Status.ShouldBe(user.Status);
    }

    [Fact]
    public async Task HandleAsync_UserFound_MapsMfaEnabledCorrectly()
    {
        // Arrange
        User user = CreateUser();
        var query = new GetUserQuery(user.Id);

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<GetUserResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.Value.MfaEnabled.ShouldBe(user.MfaEnabled);
    }

    [Fact]
    public async Task HandleAsync_PassesCancellationToken()
    {
        // Arrange
        User user = CreateUser();
        var query = new GetUserQuery(user.Id);
        using var cts = new CancellationTokenSource();

        this._userRepository.GetByIdAsync(user.Id, cts.Token)
            .Returns(user);

        // Act
        await this._handler.HandleAsync(query, cts.Token);

        // Assert
        await this._userRepository.Received(1).GetByIdAsync(user.Id, cts.Token);
    }

    private User CreateUser(string email = "user@example.com", string displayName = "Test User")
    {
        Result<User> result = User.CreateLocal(email, displayName, "hashedPassword", this._dateTimeProvider);
        return result.Value;
    }
}
