using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIdentityStack.Infrastructure.Identity;

namespace OpenIdentityStack.Api.Pages.Connect;

public class CheckSessionModel : PageModel
{
    public string SessionCookieName => SessionManagementDefaults.SessionCookieName;
}
