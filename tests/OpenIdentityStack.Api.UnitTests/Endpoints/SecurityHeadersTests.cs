using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OpenIdentityStack.Api.UnitTests.Endpoints;

public sealed class SecurityHeadersTests
{
    [Theory]
    [InlineData("/Account/Login", false)]
    [InlineData("/connect/logout", false)]
    [InlineData("/connect/check_session", true)]
    public async Task SessionIframeException_PreservesOtherPagesAndUnrelatedDirectives(string path, bool permitsEmbedding)
    {
        const string originalPolicy = "default-src 'self'; frame-ancestors 'none'; object-src 'none';";
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        app.UseSecurityHeaders(options => options.ContentSecurityPolicy = originalPolicy);
        app.Run(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        await app.Build()(context);

        string policy = context.Response.Headers.ContentSecurityPolicy.ToString();
        policy.ShouldContain("object-src 'none'");
        if (permitsEmbedding)
        {
            policy.ShouldContain("frame-ancestors *");
            policy.ShouldNotContain("frame-ancestors 'none'");
            context.Response.Headers.ContainsKey("X-Frame-Options").ShouldBeFalse();
        }
        else
        {
            policy.ShouldBe(originalPolicy);
            context.Response.Headers.XFrameOptions.ToString().ShouldBe("DENY");
        }
    }
}
