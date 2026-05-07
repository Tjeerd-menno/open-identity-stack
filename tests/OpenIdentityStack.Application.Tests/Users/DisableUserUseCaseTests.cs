using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Users;
/// <summary>
/// Unit tests for the DisableUserUseCase.
/// </summary>
public sealed class DisableUserUseCaseTests
{
    private readonly IUserRepository _userRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IDisableUserUseCase _sut;

    public DisableUserUseCaseTests()
    {
        this._userRepository = Substitute.For<IUserRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);

        this._sut = new DisableUserUseCase(this._userRepository, this._dateTimeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidUser_DisablesUser()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateActiveUser(userId);
        var command = new DisableUserCommand(userId, "Policy violation");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<DisableUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.Disabled);
        await this._userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentUser_ReturnsNotFoundError()
    {
        // Arrange
        var userId = UserId.Create();
        var command = new DisableUserCommand(userId, "Policy violation");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result<DisableUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.User.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_WithAlreadyDisabledUser_ReturnsError()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateDisabledUser(userId);
        var command = new DisableUserCommand(userId, "Another reason");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<DisableUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.User.AlreadyDisabled");
    }

    [Fact]
    public async Task ExecuteAsync_WithPendingVerificationUser_ReturnsError()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreatePendingUser(userId);
        var command = new DisableUserCommand(userId, "Policy violation");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<DisableUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.User.InvalidStatusTransition");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyReason_DisablesUser()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateActiveUser(userId);
        var command = new DisableUserCommand(userId, string.Empty);

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<DisableUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.Disabled);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDisabledAtTimestamp()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateActiveUser(userId);
        var command = new DisableUserCommand(userId, "Policy violation");
        DateTimeOffset expectedTime = DateTimeOffset.UtcNow;
        this._dateTimeProvider.UtcNow.Returns(expectedTime);

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<DisableUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.DisabledAt.ShouldBe(expectedTime);
    }

    private static User CreateActiveUser(UserId userId)
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);

        Result<User> result = User.CreateLocal(
            "test@example.com",
            "Test User",
            "hashedPassword",
            dateTimeProvider);

        User user = result.Value;
        user.VerifyEmail(dateTimeProvider);

        // Use reflection to set the Id for testing
        typeof(User).BaseType!.BaseType!
            .GetProperty("Id")!
            .SetValue(user, userId);

        return user;
    }

    private static User CreateDisabledUser(UserId userId)
    {
        User user = CreateActiveUser(userId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
        user.Disable("Previous reason", dateTimeProvider);
        return user;
    }

    private static User CreatePendingUser(UserId userId)
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);

        Result<User> result = User.CreateLocal(
            "pending@example.com",
            "Pending User",
            "hashedPassword",
            dateTimeProvider);

        User user = result.Value;

        // Use reflection to set the Id for testing
        typeof(User).BaseType!.BaseType!
            .GetProperty("Id")!
            .SetValue(user, userId);

        return user;
    }
}
