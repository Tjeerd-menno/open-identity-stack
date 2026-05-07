using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Application.Tests.Users;

public sealed class EnableUserUseCaseTests
{
    private readonly IUserRepository _userRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEnableUserUseCase _sut;
    private readonly DateTimeOffset _now = new(2026, 4, 30, 12, 0, 0, TimeSpan.Zero);

    public EnableUserUseCaseTests()
    {
        this._userRepository = Substitute.For<IUserRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(this._now);
        this._sut = new EnableUserUseCase(this._userRepository, this._dateTimeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WithDisabledUser_EnablesUserAndSavesChanges()
    {
        var userId = UserId.Create();
        User user = CreateDisabledUser(userId);
        var command = new EnableUserCommand(userId);

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        Result<EnableUserResult> result = await this._sut.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.Active);
        result.Value.UserId.ShouldBe(userId);
        result.Value.EnabledAt.ShouldBe(this._now);
        await this._userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingUser_ReturnsNotFoundAndDoesNotSave()
    {
        var userId = UserId.Create();
        var command = new EnableUserCommand(userId);

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        Result<EnableUserResult> result = await this._sut.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound);
        await this._userRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [MemberData(nameof(NonDisabledUsers))]
    public async Task ExecuteAsync_WithUserThatIsNotDisabled_ReturnsNotDisabledAndDoesNotSave(User user)
    {
        var command = new EnableUserCommand(user.Id);

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        Result<EnableUserResult> result = await this._sut.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotDisabled);
        await this._userRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    public static TheoryData<User> NonDisabledUsers()
    {
        return new TheoryData<User>
        {
            CreateActiveUser(UserId.Create()),
            CreatePendingUser(UserId.Create())
        };
    }

    private static User CreateDisabledUser(UserId userId)
    {
        User user = CreateActiveUser(userId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
        user.Disable("Disabled for test", dateTimeProvider);
        return user;
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
        SetUserId(user, userId);

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
        SetUserId(user, userId);

        return user;
    }

    private static void SetUserId(User user, UserId userId)
    {
        typeof(User).BaseType!.BaseType!
            .GetProperty("Id")!
            .SetValue(user, userId);
    }
}
