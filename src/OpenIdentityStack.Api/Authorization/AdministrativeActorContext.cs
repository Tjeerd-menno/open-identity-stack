using System.Globalization;
using System.Security.Claims;
using OpenIdentityStack.Application.Abstractions;
using SharedKernel;

namespace OpenIdentityStack.Api.Authorization;

public sealed class AdministrativeActorContext(IHttpContextAccessor accessor) : IAdministrativeActorContext
{
    public const string HumanAuthenticationClaim = "ois_human_authenticated_at";
    public const string HumanSubjectClaim = "ois_human_subject";
    public const string ApprovalHeader = "X-OIS-Administrative-Approval";

    public string AuditActorId => ResolveAuditActorId(accessor.HttpContext?.User);

    public static string ResolveAuditActorId(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true) { return "system"; }
        string? subject = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (subject is null) { return "authenticated:unknown"; }
        string[] humanProof = principal.FindAll(HumanSubjectClaim).Select(claim => claim.Value).ToArray();
        if (Guid.TryParse(subject, out Guid userId) && humanProof.Length == 1
            && string.Equals(humanProof[0], userId.ToString(), StringComparison.Ordinal))
        {
            return userId.ToString();
        }
        // Always prefix the complete machine subject, including IDs already starting with
        // "client:". AuditLogEntry bounds the encoded value without truncating identities.
        return "client:" + subject;
    }

    public AdministrativeActor? Current
    {
        get
        {
            HttpContext? context = accessor.HttpContext;
            ClaimsPrincipal? principal = context?.User;
            if (principal?.Identity?.IsAuthenticated != true ||
                !Guid.TryParse(principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
            {
                return null;
            }

            // Only an OP-generated claim from actual password/validated upstream authentication establishes freshness.
            string[] values = principal.FindAll(HumanAuthenticationClaim).Select(claim => claim.Value).ToArray();
            DateTimeOffset? authenticatedAt = null;
            if (values.Length == 1 && long.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long seconds))
            {
                try { authenticatedAt = DateTimeOffset.FromUnixTimeSeconds(seconds); }
                catch (ArgumentOutOfRangeException) { }
            }

            bool acknowledged = string.Equals(context!.Request.Headers[ApprovalHeader], "acknowledge", StringComparison.Ordinal);
            string[] subjects = principal.FindAll(HumanSubjectClaim).Select(claim => claim.Value).ToArray();
            bool isHuman = subjects.Length == 1 && string.Equals(subjects[0], userId.ToString(), StringComparison.Ordinal);
            return new AdministrativeActor(new UserId(userId), authenticatedAt, isHuman, acknowledged);
        }
    }
}
