using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIdentityStack.Api.Authentication;
using OpenIdentityStack.Api.Pages.Connect;

namespace OpenIdentityStack.Api.Tests.Authentication;

public sealed class CheckSessionModelTests
{
    [Fact]
    public async Task InvalidMonitoringCookieIsDeletedWithoutAuthenticationPrincipal()
    {
        ISessionMonitoringCookieService cookies = Substitute.For<ISessionMonitoringCookieService>();
        cookies.IsCurrentAsync("stale", Arg.Any<CancellationToken>()).Returns(false);
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "op_session=stale";
        var model = new CheckSessionModel(cookies)
        {
            PageContext = new PageContext { HttpContext = context }
        };

        await model.OnGetAsync();

        context.Response.Headers.SetCookie.ToString().ShouldContain("op_session=;");
        context.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
        context.Response.Headers.ContainsKey("X-Frame-Options").ShouldBeFalse();
    }

    [Fact]
    public async Task CurrentMonitoringCookieIsRetained()
    {
        ISessionMonitoringCookieService cookies = Substitute.For<ISessionMonitoringCookieService>();
        cookies.IsCurrentAsync("current", Arg.Any<CancellationToken>()).Returns(true);
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "op_session=current";
        var model = new CheckSessionModel(cookies)
        {
            PageContext = new PageContext { HttpContext = context }
        };

        await model.OnGetAsync();

        context.Response.Headers.SetCookie.ToString().ShouldBeEmpty();
    }
}
