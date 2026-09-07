using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Users;
/// <summary>
/// Unit tests for UnlinkUpstreamIdentityUseCase.
/// </summary>
public sealed class UnlinkUpstreamIdentityUseCaseTests
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditLog _auditLog;
    private readonly UnlinkUpstreamIdentityUseCase _useCase;

    public UnlinkUpstreamIdentityUseCaseTests()
    {
        this._userRepository = Substitute.For<IUserRepository>();
        this._auditLog = Substitute.For<IAuditLog>();

        this._useCase = new UnlinkUpstreamIdentityUseCase(this._userRepository, this._auditLog);
    }

    private static User CreateTestUserWithIdentity(
        string email = "test@example.com",
        UpstreamProviderId? providerId = null,
        string providerName = "test-provider",
        string subjectId = "ext-user-123")
    {
        UpstreamProviderId pid = providerId ?? UpstreamProviderId.Create();
        User user = User.ProvisionFederated(email, "Test User", pid, providerName, subjectId, "https://issuer.example").Value;
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
        user.SetPassword("password_hash", dateTimeProvider);
        return user;
    }

    [Fact]
    public async Task ExecuteAsync_WithValidInput_UnlinkSucceeds()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();
        User user = CreateTestUserWithIdentity(providerId: providerId);

        var command = new UnlinkUpstreamIdentityCommand(
            UserId: user.Id,
            ProviderId: providerId,
            ActorId: "admin-1");

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<UnlinkUpstreamIdentityResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        user.UpstreamIdentities.ShouldBeEmpty();
        await this._userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_UserNotFound_ReturnsError()
    {
        // Arrange
        var userId = UserId.Create();
        var providerId = UpstreamProviderId.Create();

        var command = new UnlinkUpstreamIdentityCommand(
            UserId: userId,
            ProviderId: providerId,
            ActorId: "admin-1");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result<UnlinkUpstreamIdentityResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.User.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_IdentityNotLinked_ReturnsError()
    {
        // Arrange
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
        Result<User> userResult = User.CreateLocal("test@example.com", "Test User", "password_hash", dateTimeProvider);
        User user = userResult.Value;
        
        var providerId = UpstreamProviderId.Create();

        var command = new UnlinkUpstreamIdentityCommand(
            UserId: user.Id,
            ProviderId: providerId,
            ActorId: "admin-1");

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<UnlinkUpstreamIdentityResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.UpstreamIdentity.NotLinked");
    }

    [Fact]
    public async Task ExecuteAsync_UnlinkOneOfMultiple_OnlyRemovesSpecifiedIdentity()
    {
        // Arrange
        var provider1Id = UpstreamProviderId.Create();
        var provider2Id = UpstreamProviderId.Create();

        User user = CreateTestUserWithIdentity(providerId: provider1Id, providerName: "provider1");
        user.LinkUpstreamIdentity(provider2Id, "provider2", "ext-user-456", null);

        var command = new UnlinkUpstreamIdentityCommand(
            UserId: user.Id,
            ProviderId: provider1Id,
            ActorId: "admin-1");

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<UnlinkUpstreamIdentityResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        user.UpstreamIdentities.Count.ShouldBe(1);
        user.UpstreamIdentities.ShouldContain(i => i.ProviderId == provider2Id);
        user.UpstreamIdentities.ShouldNotContain(i => i.ProviderId == provider1Id);
    }

    [Fact]
    public async Task ExecuteAsync_UnlinkLastIdentityWithPassword_Succeeds()
    {
        // Arrange - User has password, can unlink last identity
        var providerId = UpstreamProviderId.Create();
        User user = CreateTestUserWithIdentity(providerId: providerId);

        var command = new UnlinkUpstreamIdentityCommand(
            UserId: user.Id,
            ProviderId: providerId,
            ActorId: "admin-1");

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<UnlinkUpstreamIdentityResult> result = await this._useCase.ExecuteAsync(command);

        // Assert - Should succeed because user has password
        result.IsSuccess.ShouldBeTrue();
        user.UpstreamIdentities.ShouldBeEmpty();
    }
}
