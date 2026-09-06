using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Users;

/// <summary>
/// Unit tests for the DeleteUserUseCase.
/// </summary>
public sealed class DeleteUserUseCaseTests
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditLog _auditLog;
    private readonly DeleteUserUseCase _sut;

    public DeleteUserUseCaseTests()
    {
        this._userRepository = Substitute.For<IUserRepository>();
        this._auditLog = Substitute.For<IAuditLog>();

        this._sut = new DeleteUserUseCase(this._userRepository, this._auditLog);
    }

    [Fact]
    public async Task ExecuteAsync_WithExistingUser_ReturnsSuccess()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId);
        var command = new DeleteUserCommand(userId, "admin-1");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task QuarantinedIdentityBlocksDeletionAndRecordsGenericDenial()
    {
        var providerId = OpenIdentityStack.Domain.Federation.UpstreamProviderId.Create();
        User user = User.CreateFederated("retained@example.com", "Retained", providerId, "Provider", "legacy-subject", issuer: "https://issuer.example").Value;
        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        Result result = await this._sut.ExecuteAsync(new DeleteUserCommand(user.Id, "operator"));

        result.Error.ShouldBe(UpstreamIdentityErrors.QuarantineRetentionRequired);
        await this._userRepository.DidNotReceive().DeleteAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await this._auditLog.Received(1).LogAsync("operator", "User.QuarantinedIdentityDeletionDenied", "User", user.Id.Value.ToString(),
            Arg.Is<string>(details => !details.Contains(user.Email) && !details.Contains("legacy-subject")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithExistingUser_CallsDeleteAsync()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId);
        var command = new DeleteUserCommand(userId, "admin-1");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        await this._userRepository.Received(1).DeleteAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithExistingUser_SavesChanges()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId);
        var command = new DeleteUserCommand(userId, "admin-1");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        await this._userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentUser_ReturnsNotFoundError()
    {
        // Arrange
        var userId = UserId.Create();
        var command = new DeleteUserCommand(userId, "admin-1");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.User.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentUser_DoesNotCallDelete()
    {
        // Arrange
        var userId = UserId.Create();
        var command = new DeleteUserCommand(userId, "admin-1");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        await this._userRepository.DidNotReceive().DeleteAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentUser_DoesNotSaveChanges()
    {
        // Arrange
        var userId = UserId.Create();
        var command = new DeleteUserCommand(userId, "admin-1");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        await this._userRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static User CreateUser(UserId userId)
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);

        Result<User> result = User.CreateLocal(
            "test@example.com",
            "Test User",
            "hashedPassword",
            dateTimeProvider);

        User user = result.Value;

        typeof(User).BaseType!.BaseType!
            .GetProperty("Id")!
            .SetValue(user, userId);

        return user;
    }
}
