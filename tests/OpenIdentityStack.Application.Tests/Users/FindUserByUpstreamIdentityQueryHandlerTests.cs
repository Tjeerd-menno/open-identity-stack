using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Users;
/// <summary>
/// Unit tests for the FindUserByUpstreamIdentityQueryHandler.
/// </summary>
public sealed class FindUserByUpstreamIdentityQueryHandlerTests
{
    private readonly IUserRepository _userRepository;
    private readonly FindUserByUpstreamIdentityQueryHandler _handler;
    private static readonly DateTimeOffset TestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
    private static readonly UpstreamProviderId TestProviderId = UpstreamProviderId.From(Guid.NewGuid());

    public FindUserByUpstreamIdentityQueryHandlerTests()
    {
        this._userRepository = Substitute.For<IUserRepository>();
        this._handler = new FindUserByUpstreamIdentityQueryHandler(this._userRepository);
    }

    [Fact]
    public async Task HandleAsync_WithEmptySubjectId_ReturnsError()
    {
        // Arrange
        var query = new FindUserByUpstreamIdentityQuery(TestProviderId, "");

        // Act
        Result<FindUserByUpstreamIdentityResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("SubjectId");
    }

    [Fact]
    public async Task HandleAsync_WithWhitespaceSubjectId_ReturnsError()
    {
        // Arrange
        var query = new FindUserByUpstreamIdentityQuery(TestProviderId, "   ");

        // Act
        Result<FindUserByUpstreamIdentityResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("SubjectId");
    }

    [Fact]
    public async Task HandleAsync_UserNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var query = new FindUserByUpstreamIdentityQuery(TestProviderId, "subject123");

        this._userRepository.FindByUpstreamIdentityAsync(TestProviderId, "subject123", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result<FindUserByUpstreamIdentityResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("NotFound");
    }

    [Fact]
    public async Task HandleAsync_UserFoundWithMatchingIdentity_ReturnsSuccess()
    {
        // Arrange
        User user = CreateUserWithUpstreamIdentity(TestProviderId, "subject123");
        var query = new FindUserByUpstreamIdentityQuery(TestProviderId, "subject123");

        this._userRepository.FindByUpstreamIdentityAsync(TestProviderId, "subject123", Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<FindUserByUpstreamIdentityResult> result = await this._handler.HandleAsync(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.UserId.ShouldBe(user.Id);
        result.Value.Email.ShouldBe(user.Email);
        result.Value.DisplayName.ShouldBe(user.DisplayName);
    }

    [Fact]
    public async Task HandleAsync_PassesCancellationToken()
    {
        // Arrange
        var query = new FindUserByUpstreamIdentityQuery(TestProviderId, "subject123");
        using var cts = new CancellationTokenSource();

        this._userRepository.FindByUpstreamIdentityAsync(TestProviderId, "subject123", cts.Token)
            .Returns((User?)null);

        // Act
        await this._handler.HandleAsync(query, cts.Token);

        // Assert
        await this._userRepository.Received(1).FindByUpstreamIdentityAsync(TestProviderId, "subject123", cts.Token);
    }

    private static User CreateUserWithUpstreamIdentity(UpstreamProviderId providerId, string subjectId)
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(TestTime);

        Result<User> userResult = User.CreateLocal(
            "test@example.com",
            "Test User",
            "hashedpassword",
            dateTimeProvider);

        User user = userResult.Value;

        // Link upstream identity - requires providerId, providerName, subjectId, email
        user.LinkUpstreamIdentity(providerId, "test-provider", subjectId, null);

        return user;
    }
}
