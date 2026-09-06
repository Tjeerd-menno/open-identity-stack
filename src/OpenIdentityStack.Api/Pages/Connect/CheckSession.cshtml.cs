using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIdentityStack.Api.Authentication;
using OpenIdentityStack.Infrastructure.Identity;

namespace OpenIdentityStack.Api.Pages.Connect;

public class CheckSessionModel(ISessionMonitoringCookieService sessionMonitoringCookies) : PageModel
{
    public string SessionCookieName => SessionManagementDefaults.SessionCookieName;

    public async Task OnGetAsync()
    {
        // This advertised protocol endpoint must run inside the RP's cross-origin iframe.
        // Login, administrative pages, and all other responses retain the default DENY header.
        this.Response.Headers.Remove("X-Frame-Options");
        this.Response.Headers.CacheControl = "no-store";

        if (this.Request.Cookies.TryGetValue(SessionManagementDefaults.SessionCookieName, out string? value)
            && !await sessionMonitoringCookies.IsCurrentAsync(value, this.HttpContext.RequestAborted))
        {
            this.Response.Cookies.Delete(
                SessionManagementDefaults.SessionCookieName,
                SessionManagementDefaults.CreateSessionCookieOptions());
        }
    }
}
