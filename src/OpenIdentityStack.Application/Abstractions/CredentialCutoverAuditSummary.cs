namespace OpenIdentityStack.Application.Abstractions;

/// <summary>Allowlisted operational evidence without emergency identities, sessions, or resource review details.</summary>
public sealed record CredentialCutoverAuditSummary(bool Ready, DateTimeOffset EvaluatedAt, IReadOnlyList<string> BlockerCodes,
    bool EmergencyAccessUsable, int QuarantinedLinks, int AdministrativeClients, int BusinessResources,
    long OutstandingAccessTokens, DateTimeOffset? LatestAccessTokenExpiry)
{
    public static CredentialCutoverAuditSummary From(CredentialCutoverPreflight preflight) => new(
        preflight.Ready, preflight.EvaluatedAt, preflight.Blockers.Select(blocker => blocker.Code).ToArray(),
        preflight.EmergencyAccess?.CurrentlyUsable == true, preflight.Identities.QuarantinedLinks,
        preflight.AdministrativeClients.Count, preflight.BusinessResources.Count,
        preflight.OutstandingAccessTokens, preflight.LatestAccessTokenExpiry);
}
