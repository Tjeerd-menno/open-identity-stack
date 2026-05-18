using System.Net;

using OpenIdentityStack.Api.Tests.Fixtures;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Api.Tests.Users;

[Collection("AppHostFixtureCollection")]
public sealed class PublicProfilesApiTests(AppHostFixture fixture)
{
    private readonly AppHostFixture fixture = fixture;

    [Fact]
    public async Task ProfileAndAvatar_WhenUserIsActive_ReturnSuccess()
    {
        string unique = Guid.NewGuid().ToString("N");
        string preferredUsername = $"public.{unique}";
        await this.fixture.CreateTestUserAsync(
            $"public-{unique}@example.test",
            "Public Profile",
            "Password123!",
            new UserProfileData(PreferredUsername: preferredUsername));

        using HttpClient client = this.fixture.CreateClient();

        HttpResponseMessage profileResponse = await client.GetAsync($"/profiles/{preferredUsername}");
        HttpResponseMessage avatarResponse = await client.GetAsync($"/profiles/{preferredUsername}/avatar.svg");

        profileResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        avatarResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProfileAndAvatar_WhenUserCannotAuthenticate_ReturnNotFound()
    {
        string unique = Guid.NewGuid().ToString("N");
        string preferredUsername = $"disabled.{unique}";
        Guid userId = await this.fixture.CreateTestUserAsync(
            $"disabled-{unique}@example.test",
            "Disabled Profile",
            "Password123!",
            new UserProfileData(PreferredUsername: preferredUsername));
        await this.fixture.DisableUserAsync(userId);

        using HttpClient client = this.fixture.CreateClient();

        HttpResponseMessage profileResponse = await client.GetAsync($"/profiles/{preferredUsername}");
        HttpResponseMessage avatarResponse = await client.GetAsync($"/profiles/{preferredUsername}/avatar.svg");

        profileResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        avatarResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
