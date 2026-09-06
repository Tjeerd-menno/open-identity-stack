using Microsoft.AspNetCore.WebUtilities;
using OpenIdentityStack.Api.Tests.Fixtures;

namespace OpenIdentityStack.Api.Tests.Authentication;

public sealed class CheckSessionTests(AppHostFixture fixture)
{
    [Fact]
    public async Task AttackerControlledLegacyCookiesShareTheIpRateLimitPartition()
    {
        await using var isolatedFixture = new AppHostFixture($"check-session-rate-{Guid.NewGuid():N}");
        await isolatedFixture.InitializeAsync();
        var responses = new List<HttpResponseMessage>();

        for (byte value = 1; value <= 4; value++)
        {
            using HttpClient iframe = isolatedFixture.CreateClient(allowAutoRedirect: false);
            string attackerCookie = WebEncoders.Base64UrlEncode(Enumerable.Repeat(value, 32).ToArray());
            iframe.DefaultRequestHeaders.Add("Cookie", $"op_session={attackerCookie}");
            iframe.DefaultRequestHeaders.Add("X-OIS-Session-Poll", "1");
            responses.Add(await iframe.GetAsync("/connect/check_session"));
        }

        responses.Take(3).ShouldAllBe(response => response.StatusCode == System.Net.HttpStatusCode.OK);
        responses[3].StatusCode.ShouldBe(System.Net.HttpStatusCode.TooManyRequests);
        foreach (HttpResponseMessage response in responses) { response.Dispose(); }
    }

    [Fact]
    public async Task LegacyMonitoringCookieIsRetainedBeforeCredentialCutover()
    {
        using HttpClient browser = fixture.CreateClient(allowAutoRedirect: false);
        browser.DefaultRequestHeaders.Add("Cookie", "op_session=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");

        HttpResponseMessage response = await browser.GetAsync("/connect/check_session");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values).ShouldBeFalse();
    }

    [Theory]
    [InlineData("/connect/check_session", false)]
    [InlineData("/Account/Login", true)]
    [InlineData("/api/admin/users", true)]
    public async Task OnlyTheProtocolIframeAllowsEmbedding(string path, bool deny)
    {
        using HttpClient browser = fixture.CreateClient(allowAutoRedirect: false);
        HttpResponseMessage response = await browser.GetAsync(path);
        response.Headers.Contains("X-Frame-Options").ShouldBe(deny);
        if (deny) { response.Headers.GetValues("X-Frame-Options").Single().ShouldBe("DENY"); }
        else { response.Headers.CacheControl!.NoStore.ShouldBeTrue(); }
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("default-src 'self'");
    }
}
