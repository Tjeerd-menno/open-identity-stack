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
        string[] audiences = principal.FindAll("aud").Select(claim => claim.Value).ToArray();
        string[] clients = principal.FindAll("client_id").Select(claim => claim.Value).ToArray();
        string[] subjects = principal.FindAll("sub").Select(claim => claim.Value).ToArray();
        string[] scopes = principal.FindAll("scope").SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToArray();
        if (audiences.Length != 1 || audiences[0] != Audience || clients.Length != 1 || string.IsNullOrWhiteSpace(clients[0])
            || subjects.Length != 1 || !scopes.Contains(Scope, StringComparer.Ordinal))
        {
            return false;
        }
        clientId = clients[0];
        string[] humanSubjects = principal.FindAll(AdministrativeActorContext.HumanSubjectClaim).Select(claim => claim.Value).ToArray();
        if (humanSubjects.Length == 0) { return subjects[0] == clientId; }
        if (humanSubjects.Length != 1 || humanSubjects[0] != subjects[0] || !Guid.TryParse(subjects[0], out Guid id)) { return false; }
        userId = new UserId(id);
        return true;
    }
}
