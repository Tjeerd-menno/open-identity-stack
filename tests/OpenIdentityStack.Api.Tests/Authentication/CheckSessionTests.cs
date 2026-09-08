using Microsoft.AspNetCore.WebUtilities;
using OpenIdentityStack.Api.Tests.Fixtures;

namespace OpenIdentityStack.Api.Tests.Authentication;

public sealed class CheckSessionTests(AppHostFixture fixture)
{
    [Fact]
    public async Task ProtectedSessionCookiesHaveIndependentRateLimitPartitionsBehindSharedIp()
    {
        await using var isolatedFixture = new AppHostFixture($"check-session-protected-rate-{Guid.NewGuid():N}");
        await isolatedFixture.InitializeAsync();
        var responses = new List<HttpResponseMessage>();

        for (int index = 1; index <= 4; index++)
        {
            Guid userId = await isolatedFixture.CreateTestUserAsync(
                $"monitor-{index}@example.test", $"Monitor {index}", "Password123!");
            Guid sessionId = await isolatedFixture.CreateSessionAsync(userId);
            string protectedCookie = await isolatedFixture.CreateSessionMonitoringCookieAsync(userId, sessionId);
            using HttpClient iframe = isolatedFixture.CreateClient(allowAutoRedirect: false);
            iframe.DefaultRequestHeaders.Add("Cookie", $"op_session={protectedCookie}");
            iframe.DefaultRequestHeaders.Add("X-OIS-Session-Poll", "1");
            responses.Add(await iframe.GetAsync("/connect/check_session"));
        }

        responses.ShouldAllBe(response => response.StatusCode == System.Net.HttpStatusCode.OK);
        foreach (HttpResponseMessage response in responses) { response.Dispose(); }
    }

    [Fact]
    public async Task ProtectedSessionAllowsMultipleRelyingPartyPollers()
    {
        await using var isolatedFixture = new AppHostFixture($"check-session-rp-rate-{Guid.NewGuid():N}");
        await isolatedFixture.InitializeAsync();
        Guid userId = await isolatedFixture.CreateTestUserAsync(
            "multi-rp-monitor@example.test", "Multi-RP Monitor", "Password123!");
        Guid sessionId = await isolatedFixture.CreateSessionAsync(userId);
        string protectedCookie = await isolatedFixture.CreateSessionMonitoringCookieAsync(userId, sessionId);
        var responses = new List<HttpResponseMessage>();

        for (int index = 0; index < 4; index++)
        {
            using HttpClient iframe = isolatedFixture.CreateClient(allowAutoRedirect: false);
            iframe.DefaultRequestHeaders.Add("Cookie", $"op_session={protectedCookie}");
            iframe.DefaultRequestHeaders.Add("X-OIS-Session-Poll", "1");
            responses.Add(await iframe.GetAsync("/connect/check_session"));
        }

        responses.ShouldAllBe(response => response.StatusCode == System.Net.HttpStatusCode.OK);
        foreach (HttpResponseMessage response in responses) { response.Dispose(); }
    }

    [Fact]
    public async Task ProtectedSessionPartitionsRemainBoundedByAggregateIpRateLimit()
    {
        await using var isolatedFixture = new AppHostFixture($"check-session-aggregate-rate-{Guid.NewGuid():N}");
        await isolatedFixture.InitializeAsync();
        Guid userId = await isolatedFixture.CreateTestUserAsync(
            "aggregate-monitor@example.test", "Aggregate Monitor", "Password123!");
        var responses = new List<HttpResponseMessage>();

        for (int index = 0; index < 31; index++)
        {
            Guid sessionId = await isolatedFixture.CreateSessionAsync(userId);
            string protectedCookie = await isolatedFixture.CreateSessionMonitoringCookieAsync(userId, sessionId);
            using HttpClient iframe = isolatedFixture.CreateClient(allowAutoRedirect: false);
            iframe.DefaultRequestHeaders.Add("Cookie", $"op_session={protectedCookie}");
            iframe.DefaultRequestHeaders.Add("X-OIS-Session-Poll", "1");
            responses.Add(await iframe.GetAsync("/connect/check_session"));
        }

        responses.Take(30).ShouldAllBe(response => response.StatusCode == System.Net.HttpStatusCode.OK);
        responses[30].StatusCode.ShouldBe(System.Net.HttpStatusCode.TooManyRequests);
        foreach (HttpResponseMessage response in responses) { response.Dispose(); }
    }

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
    public async Task UnprotectedMonitoringCookieIsCleared()
    {
        using HttpClient browser = fixture.CreateClient(allowAutoRedirect: false);
        browser.DefaultRequestHeaders.Add("Cookie", "op_session=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");

        HttpResponseMessage response = await browser.GetAsync("/connect/check_session");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        response.Headers.GetValues("Set-Cookie").ShouldContain(value => value.StartsWith("op_session=;", StringComparison.Ordinal));
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
