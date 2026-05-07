using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Users;
/// <summary>
/// Unit tests for LinkUpstreamIdentityUseCase.
/// </summary>
public sealed class LinkUpstreamIdentityUseCaseTests
{
    private readonly IUserRepository _userRepository;
    private readonly IUpstreamProviderRepository _providerRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly LinkUpstreamIdentityUseCase _useCase;

    public LinkUpstreamIdentityUseCaseTests()
    {
        this._userRepository = Substitute.For<IUserRepository>();
        this._providerRepository = Substitute.For<IUpstreamProviderRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);

        this._useCase = new LinkUpstreamIdentityUseCase(
            this._userRepository,
            this._providerRepository,
            this._dateTimeProvider);
    }

    private static User CreateTestUser(string email = "test@example.com")
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
        Result<User> result = User.CreateLocal(email, "Test User", "password_hash", dateTimeProvider);
        return result.Value;
    }

    private static UpstreamProvider CreateTestProvider(string name = "test-provider")
    {
        Result<UpstreamProvider> result = UpstreamProvider.Create(
            name,
            "Test Provider",
            "https://provider.example.com",
            "client_id");
        return result.Value;
    }

    [Fact]
    public async Task ExecuteAsync_WithValidInput_LinkSucceeds()
    {
        // Arrange
        User user = CreateTestUser();
        UpstreamProvider provider = CreateTestProvider();

        var command = new LinkUpstreamIdentityCommand(
            UserId: user.Id,
            ProviderId: provider.Id,
            ProviderName: provider.Name,
            SubjectId: "ext-user-123",
            Email: "external@example.com");

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        this._providerRepository.GetByIdAsync(provider.Id, Arg.Any<CancellationToken>())
            .Returns(provider);
        this._userRepository.FindByUpstreamIdentityAsync(provider.Id, "ext-user-123", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result<LinkUpstreamIdentityResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.UserId.ShouldBe(user.Id);
        result.Value.ProviderId.ShouldBe(provider.Id);
        user.UpstreamIdentities.ShouldContain(i => i.SubjectId == "ext-user-123");
        await this._userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_UserNotFound_ReturnsError()
    {
        // Arrange
        UpstreamProvider provider = CreateTestProvider();
        var userId = UserId.Create();

        var command = new LinkUpstreamIdentityCommand(
            UserId: userId,
            ProviderId: provider.Id,
            ProviderName: provider.Name,
            SubjectId: "ext-user-123",
            Email: null);

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result<LinkUpstreamIdentityResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.User.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_ProviderNotFound_ReturnsError()
    {
        // Arrange
        User user = CreateTestUser();
        var providerId = UpstreamProviderId.Create();

        var command = new LinkUpstreamIdentityCommand(
            UserId: user.Id,
            ProviderId: providerId,
            ProviderName: "unknown-provider",
            SubjectId: "ext-user-123",
            Email: null);

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        this._providerRepository.GetByIdAsync(providerId, Arg.Any<CancellationToken>())
            .Returns((UpstreamProvider?)null);

        // Act
        Result<LinkUpstreamIdentityResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.UpstreamProvider.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_IdentityAlreadyLinkedToAnotherUser_ReturnsError()
    {
        // Arrange
        User user = CreateTestUser();
        User existingUser = CreateTestUser("other@example.com");
        UpstreamProvider provider = CreateTestProvider();

        var command = new LinkUpstreamIdentityCommand(
            UserId: user.Id,
            ProviderId: provider.Id,
            ProviderName: provider.Name,
            SubjectId: "ext-user-123",
            Email: null);

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        this._providerRepository.GetByIdAsync(provider.Id, Arg.Any<CancellationToken>())
            .Returns(provider);
        this._userRepository.FindByUpstreamIdentityAsync(provider.Id, "ext-user-123", Arg.Any<CancellationToken>())
            .Returns(existingUser);

        // Act
        Result<LinkUpstreamIdentityResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.UpstreamIdentity.AlreadyLinked");
    }

    [Fact]
    public async Task ExecuteAsync_RelinkSameProviderToSameUser_ReturnsAlreadyLinked()
    {
        // Arrange
        User user = CreateTestUser();
        UpstreamProvider provider = CreateTestProvider();

        // First link - already linked to same user
        user.LinkUpstreamIdentity(provider.Id, provider.Name, "ext-user-123", null);

        var command = new LinkUpstreamIdentityCommand(
            UserId: user.Id,
            ProviderId: provider.Id,
            ProviderName: provider.Name,
            SubjectId: "ext-user-123",
            Email: "updated@example.com");

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        this._providerRepository.GetByIdAsync(provider.Id, Arg.Any<CancellationToken>())
            .Returns(provider);
        this._userRepository.FindByUpstreamIdentityAsync(provider.Id, "ext-user-123", Arg.Any<CancellationToken>())
            .Returns(user); // Same user - but domain rejects re-linking

        // Act
        Result<LinkUpstreamIdentityResult> result = await this._useCase.ExecuteAsync(command);

        // Assert - Domain prevents linking same provider twice, even for same user
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.UpstreamIdentity.AlreadyLinked");
    }

    [Fact]
    public async Task ExecuteAsync_LinkMultipleProviders_Succeeds()
    {
        // Arrange
        User user = CreateTestUser();
        UpstreamProvider provider1 = CreateTestProvider("provider1");
        UpstreamProvider provider2 = CreateTestProvider("provider2");

        // First link
        user.LinkUpstreamIdentity(provider1.Id, provider1.Name, "ext-user-123", null);

        var command = new LinkUpstreamIdentityCommand(
            UserId: user.Id,
            ProviderId: provider2.Id,
            ProviderName: provider2.Name,
            SubjectId: "ext-user-456",
            Email: null);

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        this._providerRepository.GetByIdAsync(provider2.Id, Arg.Any<CancellationToken>())
            .Returns(provider2);
        this._userRepository.FindByUpstreamIdentityAsync(provider2.Id, "ext-user-456", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result<LinkUpstreamIdentityResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        user.UpstreamIdentities.Count.ShouldBe(2);
    }
}
