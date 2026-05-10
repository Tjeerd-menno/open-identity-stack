using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Users;

/// <summary>
/// Unit tests for the UpdateUserUseCase.
/// </summary>
public sealed class UpdateUserUseCaseTests
{
    private readonly IUserRepository _userRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly DateTimeOffset _now = new(2026, 1, 18, 12, 0, 0, TimeSpan.Zero);
    private readonly UpdateUserUseCase _sut;

    public UpdateUserUseCaseTests()
    {
        this._userRepository = Substitute.For<IUserRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(this._now);

        this._sut = new UpdateUserUseCase(this._userRepository, this._dateTimeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidDisplayName_ReturnsSuccess()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId, "Old Name");
        var command = new UpdateUserCommand(userId, "New Name");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<UpdateUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.UserId.ShouldBe(userId);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidDisplayName_UpdatesDisplayName()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId, "Old Name");
        var command = new UpdateUserCommand(userId, "New Name");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        user.DisplayName.ShouldBe("New Name");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidDisplayName_SavesChanges()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId, "Old Name");
        var command = new UpdateUserCommand(userId, "New Name");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        await this._userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsUpdatedAtFromDateTimeProvider()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId, "Old Name");
        var command = new UpdateUserCommand(userId, "New Name");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<UpdateUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.UpdatedAt.ShouldBe(this._now);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullDisplayName_SkipsDisplayNameUpdate()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId, "Original Name");
        var command = new UpdateUserCommand(userId, null);

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<UpdateUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        user.DisplayName.ShouldBe("Original Name");
    }

    [Fact]
    public async Task ExecuteAsync_WithWhitespaceDisplayName_SkipsDisplayNameUpdate()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId, "Original Name");
        var command = new UpdateUserCommand(userId, "   ");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<UpdateUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        user.DisplayName.ShouldBe("Original Name");
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentUser_ReturnsNotFoundError()
    {
        // Arrange
        var userId = UserId.Create();
        var command = new UpdateUserCommand(userId, "New Name");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result<UpdateUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.User.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentUser_DoesNotSaveChanges()
    {
        // Arrange
        var userId = UserId.Create();
        var command = new UpdateUserCommand(userId, "New Name");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        await this._userRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithTooLongDisplayName_ReturnsValidationError()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId, "Old Name");
        string tooLongName = new string('a', 257);
        var command = new UpdateUserCommand(userId, tooLongName);

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<UpdateUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.User.DisplayNameTooLong");
    }

    private User CreateUser(UserId userId, string displayName)
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(this._now);

        Result<User> result = User.CreateLocal(
            "test@example.com",
            displayName,
            "hashedPassword",
            dateTimeProvider);

        User user = result.Value;

        typeof(User).BaseType!.BaseType!
            .GetProperty("Id")!
            .SetValue(user, userId);

        return user;
    }
}
