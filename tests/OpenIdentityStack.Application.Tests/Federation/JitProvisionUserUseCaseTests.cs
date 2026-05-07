using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Federation.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Federation;
/// <summary>
/// Unit tests for the JitProvisionUserUseCase.
/// </summary>
public sealed class JitProvisionUserUseCaseTests
{
    private readonly IUserRepository _userRepository;
    private readonly IUpstreamProviderRepository _providerRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IJitProvisionUserUseCase _sut;
    private static readonly IDateTimeProvider StaticDateTimeProvider = new TestDateTimeProviderImpl();

    private sealed class TestDateTimeProviderImpl : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateTimeOffset Now => DateTimeOffset.Now;
    }

    public JitProvisionUserUseCaseTests()
    {
        this._userRepository = Substitute.For<IUserRepository>();
        this._providerRepository = Substitute.For<IUpstreamProviderRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);

        this._sut = new JitProvisionUserUseCase(
            this._userRepository,
            this._providerRepository,
            this._dateTimeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WithNewUser_CreatesUserWithUpstreamIdentity()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();
        UpstreamProvider provider = CreateActiveProvider(providerId);
        var command = new JitProvisionUserCommand(
            providerId,
            "upstream-subject-123",
            "user@example.com",
            "John Doe");

        this._providerRepository.GetByIdAsync(providerId, Arg.Any<CancellationToken>())
            .Returns(provider);
        this._userRepository.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);
        this._userRepository.FindByUpstreamIdentityAsync(providerId, "upstream-subject-123", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result<JitProvisionUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.IsNewUser.ShouldBeTrue();
        result.Value.UserId.ShouldNotBe(default);
        await this._userRepository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await this._userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithExistingUpstreamIdentity_ReturnsExistingUser()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();
        UpstreamProvider provider = CreateActiveProvider(providerId);
        User existingUser = CreateFederatedUser(providerId);
        var command = new JitProvisionUserCommand(
            providerId,
            "upstream-subject-123",
            "user@example.com",
            "John Doe");

        this._providerRepository.GetByIdAsync(providerId, Arg.Any<CancellationToken>())
            .Returns(provider);
        this._userRepository.FindByUpstreamIdentityAsync(providerId, "upstream-subject-123", Arg.Any<CancellationToken>())
            .Returns(existingUser);

        // Act
        Result<JitProvisionUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
            result.Value.IsNewUser.ShouldBeFalse();
            result.Value.UserId.ShouldBe(existingUser.Id);
            await this._userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentProvider_ReturnsError()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();
        var command = new JitProvisionUserCommand(
            providerId,
            "upstream-subject-123",
            "user@example.com",
            "John Doe");

        this._providerRepository.GetByIdAsync(providerId, Arg.Any<CancellationToken>())
            .Returns((UpstreamProvider?)null);

        // Act
        Result<JitProvisionUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("NotFound"); // Prefixed error code: NotFound.UpstreamProvider.NotFound
    }

    [Fact]
    public async Task ExecuteAsync_WithDisabledProvider_ReturnsError()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();
        UpstreamProvider provider = CreateDisabledProvider(providerId);
        var command = new JitProvisionUserCommand(
            providerId,
            "upstream-subject-123",
            "user@example.com",
            "John Doe");

        this._providerRepository.GetByIdAsync(providerId, Arg.Any<CancellationToken>())
            .Returns(provider);

        // Act
        Result<JitProvisionUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("Disabled"); // Prefixed error code: Forbidden.UpstreamProvider.Disabled
    }

    [Fact]
    public async Task ExecuteAsync_WithExistingEmailUser_LinksUpstreamIdentity()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();
        UpstreamProvider provider = CreateActiveProvider(providerId);
        User existingUser = CreateLocalUser();
        var command = new JitProvisionUserCommand(
            providerId,
            "upstream-subject-123",
            "existing@example.com",
            "John Doe");

        this._providerRepository.GetByIdAsync(providerId, Arg.Any<CancellationToken>())
            .Returns(provider);
        this._userRepository.FindByUpstreamIdentityAsync(providerId, "upstream-subject-123", Arg.Any<CancellationToken>())
            .Returns((User?)null);
        this._userRepository.GetByEmailAsync("existing@example.com", Arg.Any<CancellationToken>())
            .Returns(existingUser);

        // Act
        Result<JitProvisionUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.IsNewUser.ShouldBeFalse();
        result.Value.UserId.ShouldBe(existingUser.Id);
        await this._userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await this._userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptySubjectId_ReturnsError()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();
        var command = new JitProvisionUserCommand(
            providerId,
            string.Empty,
            "user@example.com",
            "John Doe");

        // Act
        Result<JitProvisionUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("SubjectIdRequired");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyEmail_CreatesUserWithoutEmail()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();
        UpstreamProvider provider = CreateActiveProvider(providerId);
        var command = new JitProvisionUserCommand(
            providerId,
            "upstream-subject-123",
            null,
            "John Doe");

        this._providerRepository.GetByIdAsync(providerId, Arg.Any<CancellationToken>())
            .Returns(provider);
        this._userRepository.FindByUpstreamIdentityAsync(providerId, "upstream-subject-123", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result<JitProvisionUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.IsNewUser.ShouldBeTrue();
        await this._userRepository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyDisplayName_UsesEmailAsDisplayName()
    {
        // Arrange
        var providerId = UpstreamProviderId.Create();
        UpstreamProvider provider = CreateActiveProvider(providerId);
        var command = new JitProvisionUserCommand(
            providerId,
            "upstream-subject-123",
            "user@example.com",
            null);

        this._providerRepository.GetByIdAsync(providerId, Arg.Any<CancellationToken>())
            .Returns(provider);
        this._userRepository.FindByUpstreamIdentityAsync(providerId, "upstream-subject-123", Arg.Any<CancellationToken>())
            .Returns((User?)null);
        this._userRepository.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result<JitProvisionUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.IsNewUser.ShouldBeTrue();
    }

    private static UpstreamProvider CreateActiveProvider(UpstreamProviderId id)
    {
        UpstreamProvider provider = UpstreamProvider.Create(
            "azure-ad",
            "Azure AD",
            "https://login.microsoftonline.com/tenant/v2.0",
            "client-id").Value;

        // Use reflection to set the ID for testing
        typeof(UpstreamProvider)
            .GetProperty(nameof(UpstreamProvider.Id))!
            .SetValue(provider, id);

        return provider;
    }

    private static UpstreamProvider CreateDisabledProvider(UpstreamProviderId id)
    {
        UpstreamProvider provider = CreateActiveProvider(id);
        provider.Disable();
        return provider;
    }

    private static User CreateFederatedUser(UpstreamProviderId providerId)
    {
        User user = User.CreateFederated(
            "user@example.com",
            "John Doe",
            providerId,
            "azure-ad",
            "upstream-subject-123").Value;
        return user;
    }

    private static User CreateLocalUser()
    {
        User user = User.CreateLocal(
            "existing@example.com",
            "Existing User",
            "hashed-password",
            StaticDateTimeProvider).Value;
        return user;
    }
}
