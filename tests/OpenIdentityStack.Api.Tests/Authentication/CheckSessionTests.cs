using OpenIdentityStack.Api.Tests.Fixtures;

namespace OpenIdentityStack.Api.Tests.Authentication;

public sealed class CheckSessionTests(AppHostFixture fixture)
{
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
