using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OpenIdentityStack.Application.Sessions.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Identity;
using SharedKernel;

namespace OpenIdentityStack.Api.Authentication;

public static class SessionCookieValidation
{
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        IServiceProvider services = context.HttpContext.RequestServices;
        string? subject = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        string? session = context.Principal?.FindFirstValue("sid");
        bool valid;
        if (Guid.TryParse(subject, out Guid userId) && Guid.TryParse(session, out Guid sessionId))
        {
            IDateTimeProvider clock = services.GetRequiredService<IDateTimeProvider>();
            Result<GetUserResult> user = await services.GetRequiredService<IGetUserQueryHandler>()
                .HandleAsync(new GetUserQuery(new UserId(userId)), context.HttpContext.RequestAborted);
            GetSessionResult? persisted = await services.GetRequiredService<IGetSessionQueryHandler>()
                .HandleAsync(new GetSessionQuery(new SessionId(sessionId)), context.HttpContext.RequestAborted);
            valid = user.IsSuccess && user.Value.Status == UserStatus.Active
                && persisted?.UserId.Value == userId
                && persisted.Status == SessionStatus.Active
                && persisted.ExpiresAt >= clock.UtcNow;
        }
        else { valid = false; }
        if (!valid)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync("Cookies");
            context.HttpContext.Response.Cookies.Delete(SessionManagementDefaults.SessionCookieName,
                SessionManagementDefaults.CreateSessionCookieOptions());
        }
    }
}
