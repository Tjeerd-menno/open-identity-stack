using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Users;

/// <summary>
/// Unit tests for ListUserUpstreamIdentitiesQueryHandler.
/// </summary>
public sealed class ListUserUpstreamIdentitiesQueryHandlerTests
{
    private readonly IUserRepository _userRepository;
    private readonly ListUserUpstreamIdentitiesQueryHandler _handler;
    private readonly IDateTimeProvider _dateTimeProvider;
    private static readonly DateTimeOffset TestTime = new(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly UpstreamProviderId TestProviderId = UpstreamProviderId.From(Guid.NewGuid());

    public ListUserUpstreamIdentitiesQueryHandlerTests()
    {
        this._userRepository = Substitute.For<IUserRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(TestTime);
        this._handler = new ListUserUpstreamIdentitiesQueryHandler(this._userRepository);
    }

    [Fact]
    public async Task ExecuteAsync_UserNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var userId = new UserId(Guid.NewGuid());
        var query = new ListUserUpstreamIdentitiesQuery(userId);

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result<ListUserUpstreamIdentitiesResult> result = await this._handler.ExecuteAsync(query);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.User.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_UserWithNoUpstreamIdentities_ReturnsEmptyList()
    {
        // Arrange
        User user = CreateUser();
        var query = new ListUserUpstreamIdentitiesQuery(user.Id);

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<ListUserUpstreamIdentitiesResult> result = await this._handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_UserWithOneUpstreamIdentity_ReturnsSingleItem()
    {
        // Arrange
        User user = CreateUserWithUpstreamIdentity(TestProviderId, "sub-001");
        var query = new ListUserUpstreamIdentitiesQuery(user.Id);

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<ListUserUpstreamIdentitiesResult> result = await this._handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_UserWithUpstreamIdentity_MapsProviderIdCorrectly()
    {
        // Arrange
        User user = CreateUserWithUpstreamIdentity(TestProviderId, "sub-001");
        var query = new ListUserUpstreamIdentitiesQuery(user.Id);

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<ListUserUpstreamIdentitiesResult> result = await this._handler.ExecuteAsync(query);

        // Assert
        result.Value.Items[0].ProviderId.ShouldBe(TestProviderId);
    }

    [Fact]
    public async Task ExecuteAsync_UserWithUpstreamIdentity_MapsSubjectIdCorrectly()
    {
        // Arrange
        User user = CreateUserWithUpstreamIdentity(TestProviderId, "sub-001");
        var query = new ListUserUpstreamIdentitiesQuery(user.Id);

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<ListUserUpstreamIdentitiesResult> result = await this._handler.ExecuteAsync(query);

        // Assert
        result.Value.Items[0].SubjectId.ShouldBe("sub-001");
    }

    [Fact]
    public async Task ExecuteAsync_UserWithUpstreamIdentity_MapsProviderNameCorrectly()
    {
        // Arrange
        User user = CreateUserWithUpstreamIdentity(TestProviderId, "sub-001", providerName: "azure-ad");
        var query = new ListUserUpstreamIdentitiesQuery(user.Id);

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<ListUserUpstreamIdentitiesResult> result = await this._handler.ExecuteAsync(query);

        // Assert
        result.Value.Items[0].ProviderName.ShouldBe("azure-ad");
    }

    [Fact]
    public async Task ExecuteAsync_UserWithUpstreamIdentityEmail_MapsEmailCorrectly()
    {
        // Arrange
        User user = CreateUserWithUpstreamIdentity(TestProviderId, "sub-001", email: "federated@example.com");
        var query = new ListUserUpstreamIdentitiesQuery(user.Id);

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<ListUserUpstreamIdentitiesResult> result = await this._handler.ExecuteAsync(query);

        // Assert
        result.Value.Items[0].Email.ShouldBe("federated@example.com");
    }

    [Fact]
    public async Task ExecuteAsync_UserWithMultipleUpstreamIdentities_ReturnsAllItems()
    {
        // Arrange
        var providerId2 = UpstreamProviderId.From(Guid.NewGuid());
        User user = CreateUserWithUpstreamIdentity(TestProviderId, "sub-001");
        user.LinkUpstreamIdentity(providerId2, "google", "sub-002", null);

        var query = new ListUserUpstreamIdentitiesQuery(user.Id);

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<ListUserUpstreamIdentitiesResult> result = await this._handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationToken()
    {
        // Arrange
        User user = CreateUser();
        var query = new ListUserUpstreamIdentitiesQuery(user.Id);
        using var cts = new CancellationTokenSource();

        this._userRepository.GetByIdAsync(user.Id, cts.Token)
            .Returns(user);

        // Act
        await this._handler.ExecuteAsync(query, cts.Token);

        // Assert
        await this._userRepository.Received(1).GetByIdAsync(user.Id, cts.Token);
    }

    private User CreateUser(string email = "user@example.com", string displayName = "Test User")
    {
        Result<User> result = User.CreateLocal(email, displayName, "hashedPassword", this._dateTimeProvider);
        return result.Value;
    }

    private User CreateUserWithUpstreamIdentity(
        UpstreamProviderId providerId,
        string subjectId,
        string providerName = "test-provider",
        string? email = null)
    {
        User user = CreateUser();
        user.LinkUpstreamIdentity(providerId, providerName, subjectId, email);
        return user;
    }
}
