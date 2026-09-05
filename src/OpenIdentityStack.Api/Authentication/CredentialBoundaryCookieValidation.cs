using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Common;
using SharedKernel;

namespace OpenIdentityStack.Api.Authentication;

public static class CredentialBoundaryCookieValidation
{
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        IServiceProvider services = context.HttpContext.RequestServices;
        ICredentialBoundaryStore boundary = services.GetRequiredService<ICredentialBoundaryStore>();
        string? subject = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        string? session = context.Principal?.FindFirstValue("sid");
        bool valid = await boundary.IsCurrentAsync(context.Principal?.FindFirstValue(CredentialBoundaryClaims.Epoch), context.HttpContext.RequestAborted);
        if (valid && Guid.TryParse(subject, out Guid userId) && Guid.TryParse(session, out Guid sessionId))
        {
            OpenIdentityStack.Domain.Users.User? user = await services.GetRequiredService<IUserRepository>().GetByIdAsync(new UserId(userId), context.HttpContext.RequestAborted);
            UserSession? persisted = await services.GetRequiredService<ISessionRepository>().GetByIdAsync(new SessionId(sessionId), context.HttpContext.RequestAborted);
            valid = user?.CanAuthenticate() == true && persisted?.UserId.Value == userId && persisted.Status == SessionStatus.Active
                && !persisted.IsExpired(services.GetRequiredService<IDateTimeProvider>());
        }
        else { valid = false; }
        if (!valid)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync("Cookies");
        }
    }
}
