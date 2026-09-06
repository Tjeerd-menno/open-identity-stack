using OpenIdentityStack.Api.Tests.Fixtures;

namespace OpenIdentityStack.Api.Tests.Authentication;

public sealed class CheckSessionTests(AppHostFixture fixture)
{
    [Fact]
    public async Task LegacyMonitoringCookieIsClearedWithoutAuthenticationCookie()
    {
        using HttpClient browser = fixture.CreateClient(allowAutoRedirect: false);
        browser.DefaultRequestHeaders.Add("Cookie", "op_session=legacy-random-value");

        HttpResponseMessage response = await browser.GetAsync("/connect/check_session");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        string cleared = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("op_session=", StringComparison.Ordinal));
        cleared.ShouldContain("op_session=;");
        cleared.ShouldContain("samesite=none");
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
