using System.Net;

namespace OpenIdentityStack.Testing;

public sealed class AspireTestApplicationTests
{
    [Fact]
    public async Task WaitForHttpReadyAsync_RedirectResponse_ReturnsWithoutTimingOut()
    {
        using HttpClient client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Redirect));

        await Should.NotThrowAsync(() =>
            AspireTestApplication.WaitForHttpReadyAsync(client, TimeSpan.FromSeconds(1), "/ready"));
    }

    [Fact]
    public async Task WaitForHttpReadyAsync_NotFoundResponse_TimesOutWithLastStatusCode()
    {
        using HttpClient client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        TimeoutException exception = await Should.ThrowAsync<TimeoutException>(() =>
            AspireTestApplication.WaitForHttpReadyAsync(client, TimeSpan.FromMilliseconds(300), "/missing"));

        exception.Message.ShouldContain("404");
        exception.Message.ShouldContain("NotFound");
        exception.Message.ShouldContain("/missing");
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        return new HttpClient(new MockHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("http://localhost")
        };
    }

    private sealed class MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
