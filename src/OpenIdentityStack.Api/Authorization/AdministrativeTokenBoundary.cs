using System.Security.Claims;
using SharedKernel;
using OpenIdentityStack.Domain.Resources;

namespace OpenIdentityStack.Api.Authorization;

internal static class AdministrativeTokenBoundary
{
    public const string Audience = ProtectedResource.AdministrativeAudience;
    public const string Scope = ProtectedResource.AdministrativeScope;

    public static bool TryRead(ClaimsPrincipal principal, out string clientId, out UserId? userId)
    {
        clientId = string.Empty;
        userId = null;
        if (principal.Identity?.IsAuthenticated != true) { return false; }
        if (principal.FindSingleClaim("aud")?.Value != Audience) { return false; }

        string? client = principal.FindSingleClaim("client_id")?.Value;
        if (string.IsNullOrWhiteSpace(client)) { return false; }

        string? subject = principal.FindSingleClaim("sub")?.Value;
        if (subject is null || !principal.ContainsScopeValue(Scope)) { return false; }

        clientId = client;
        Claim? humanSubject = principal.FindSingleClaim(AdministrativeActorContext.HumanSubjectClaim);
        if (humanSubject is null)
        {
            return subject == clientId;
        }

        if (humanSubject.Value != subject || !Guid.TryParse(subject, out Guid id)) { return false; }
        userId = new UserId(id);
        return true;
    }
}
