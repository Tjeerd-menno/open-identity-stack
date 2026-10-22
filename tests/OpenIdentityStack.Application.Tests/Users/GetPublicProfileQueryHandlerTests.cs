using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Users;

public sealed class GetPublicProfileQueryHandlerTests
{
    private readonly IUserRepository userRepository = Substitute.For<IUserRepository>();
    private readonly IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly GetPublicProfileQueryHandler handler;

    public GetPublicProfileQueryHandlerTests()
    {
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero));
        this.handler = new GetPublicProfileQueryHandler(this.userRepository);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsMissing_ReturnsNull()
    {
        this.userRepository.GetByPreferredUsernameAsync("missing.user", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        PublicProfileResult? result = await this.handler.HandleAsync(new GetPublicProfileQuery("missing.user"));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenUserCannotAuthenticate_ReturnsNull()
    {
        User user = this.CreateUser();
        user.VerifyEmail(this.dateTimeProvider);
        user.Disable("disabled", this.dateTimeProvider);
        this.userRepository.GetByPreferredUsernameAsync("ada.lovelace", Arg.Any<CancellationToken>())
            .Returns(user);

        PublicProfileResult? result = await this.handler.HandleAsync(new GetPublicProfileQuery("ada.lovelace"));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenUserCanAuthenticate_ReturnsProfileFields()
    {
        User user = this.CreateUser();
        user.VerifyEmail(this.dateTimeProvider);
        user.UpdateProfile(
            new UserProfileData(
                GivenName: "Ada",
                FamilyName: "Lovelace",
                PreferredUsername: "ada.lovelace",
                Website: "https://example.test",
                ZoneInfo: "Europe/Amsterdam",
                Locale: "en"),
            this.dateTimeProvider);
        this.userRepository.GetByPreferredUsernameAsync("ada.lovelace", Arg.Any<CancellationToken>())
            .Returns(user);

        PublicProfileResult? result = await this.handler.HandleAsync(new GetPublicProfileQuery("ada.lovelace"));

        result.ShouldNotBeNull();
        result.DisplayName.ShouldBe("Ada Lovelace");
        result.GivenName.ShouldBe("Ada");
        result.FamilyName.ShouldBe("Lovelace");
        result.Locale.ShouldBe("en");
        result.ZoneInfo.ShouldBe("Europe/Amsterdam");
        result.Website.ShouldBe("https://example.test");
        result.PreferredUsername.ShouldBe("ada.lovelace");
    }

    private User CreateUser()
    {
        Result<User> result = User.CreateLocal(
            "ada@example.test",
            "Ada Lovelace",
            "hashedPassword",
            this.dateTimeProvider);
        return result.Value;
    }
}
