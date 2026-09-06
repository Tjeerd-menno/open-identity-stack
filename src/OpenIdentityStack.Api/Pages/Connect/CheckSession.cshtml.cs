using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIdentityStack.Infrastructure.Identity;

namespace OpenIdentityStack.Api.Pages.Connect;

public class CheckSessionModel : PageModel
{
    public string SessionCookieName => SessionManagementDefaults.SessionCookieName;

    public void OnGet()
    {
        // This advertised protocol endpoint must run inside the RP's cross-origin iframe.
        // Login, administrative pages, and all other responses retain the default DENY header.
        this.Response.Headers.Remove("X-Frame-Options");
        this.Response.Headers.CacheControl = "no-store";
    }
}
