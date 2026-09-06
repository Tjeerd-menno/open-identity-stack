using OpenIdentityStack.Api.Tests.Fixtures;

namespace OpenIdentityStack.Api.Tests.Authentication;

public sealed class CheckSessionTests(AppHostFixture fixture)
{
    [Fact]
    public async Task MonitoringPollsAcrossIframeClientsAreRateLimitedBySession()
    {
        using HttpClient firstIframe = fixture.CreateClient(allowAutoRedirect: false);
        using HttpClient secondIframe = fixture.CreateClient(allowAutoRedirect: false);
        const string cookie = "op_session=rate-limit-session";
        firstIframe.DefaultRequestHeaders.Add("Cookie", cookie);
        secondIframe.DefaultRequestHeaders.Add("Cookie", cookie);
        firstIframe.DefaultRequestHeaders.Add("X-OIS-Session-Poll", "1");
        secondIframe.DefaultRequestHeaders.Add("X-OIS-Session-Poll", "1");

        (await firstIframe.GetAsync("/connect/check_session")).StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        (await secondIframe.GetAsync("/connect/check_session")).StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        (await firstIframe.GetAsync("/connect/check_session")).StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        (await secondIframe.GetAsync("/connect/check_session")).StatusCode
            .ShouldBe(System.Net.HttpStatusCode.TooManyRequests);

        using HttpClient otherSession = fixture.CreateClient(allowAutoRedirect: false);
        otherSession.DefaultRequestHeaders.Add("Cookie", "op_session=other-rate-limit-session");
        otherSession.DefaultRequestHeaders.Add("X-OIS-Session-Poll", "1");
        (await otherSession.GetAsync("/connect/check_session")).StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
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
