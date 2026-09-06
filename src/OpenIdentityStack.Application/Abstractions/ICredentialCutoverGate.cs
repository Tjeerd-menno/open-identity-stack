using SharedKernel;

namespace OpenIdentityStack.Application.Abstractions;

public sealed record CutoverBlocker(string Code, string Message, int Count = 1);
public sealed record EmergencyAccessEvidence(Guid Id, Guid UserId, Guid SessionId, DateTimeOffset AuthenticatedAt, DateTimeOffset RecordedAt, bool CurrentlyUsable);
public sealed record CutoverIdentityInventory(int QuarantinedLinks, int AffectedUsers, int FederationOnlyUsers, int PasswordCandidates, int DisabledUsers, int VerifiedEmails, int ProviderEvidence, int WithdrawnEvidence);
public sealed record CutoverAdministrativeClient(Guid Id, string ClientId, bool Active, bool Approved, IReadOnlyList<string> DelegatedPermissions, IReadOnlyList<string> ApplicationPermissions, bool RequiresMigrationReview);
public sealed record ResourceTokenWindow(Guid ResourceId, string DisplayName, string Audience, string Scope, long Revision,
    string? Mechanism, int? ResidualSeconds, string? EvidenceReference, DateTimeOffset? ReviewedAt, bool Reviewed);
public sealed record ResourceWindowReview(Guid ResourceId, string Mechanism, int ResidualSeconds, string EvidenceReference);
public sealed record CredentialCutoverPreflight(Guid Epoch, DateTimeOffset EvaluatedAt, IReadOnlyList<CutoverBlocker> Blockers,
    EmergencyAccessEvidence? EmergencyAccess, CutoverIdentityInventory Identities, IReadOnlyList<CutoverAdministrativeClient> AdministrativeClients,
    IReadOnlyList<ResourceTokenWindow> BusinessResources, long OutstandingAccessTokens, DateTimeOffset? LatestAccessTokenExpiry)
{
    public bool Ready => this.Blockers.Count == 0;
}

public interface ICredentialCutoverGate
{
    Task<CredentialCutoverPreflight> EvaluateAsync(CancellationToken cancellationToken = default);
}

public interface ICredentialCutoverReadinessStore : ICredentialCutoverGate
{
    Task<Result<EmergencyAccessEvidence>> RecordEmergencyAccessAsync(AdministrativeActor actor, CancellationToken cancellationToken = default);
    Task<Result> ReviewResourceWindowAsync(ResourceWindowReview review, string actorId, CancellationToken cancellationToken = default);
}

public sealed class CredentialCutoverReadiness(
    ICredentialCutoverReadinessStore store, IAdministrativeApproval approval, IAdministrativeActorContext actor, IAuditLog audit)
{
    public async Task<CredentialCutoverPreflight> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        CredentialCutoverPreflight result = await store.EvaluateAsync(cancellationToken);
        await audit.LogChangeAsync(actor.AuditActorId, "CredentialCutover.PreflightEvaluated", "CredentialBoundary",
            result.Epoch.ToString(), null, System.Text.Json.JsonSerializer.Serialize(CredentialCutoverAuditSummary.From(result)), cancellationToken);
        return result;
    }

    public async Task<Result<EmergencyAccessEvidence>> RecordEmergencyAccessAsync(CancellationToken cancellationToken = default)
    {
        Result approved = await approval.RequireAsync("CredentialCutover.RecordEmergencyAccess", "current-operator", cancellationToken: cancellationToken);
        if (approved.IsFailure) { return approved.Error; }
        AdministrativeActor? current = actor.Current;
        if (current is null || !current.IsHuman || current.LocalPasswordSessionId is null || current.CredentialEpoch is null)
        {
            await approval.RecordOutcomeAsync(false, cancellationToken);
            return DomainError.Forbidden("CredentialCutover.IndependentLoginRequired", "Sign in with the emergency administrator's local password before recording access evidence.");
        }
        Result<EmergencyAccessEvidence> result = await store.RecordEmergencyAccessAsync(current, cancellationToken);
        await approval.RecordOutcomeAsync(result.IsSuccess, cancellationToken);
        return result;
    }

    public async Task<Result> ReviewResourceWindowAsync(ResourceWindowReview review, CancellationToken cancellationToken = default)
    {
        await approval.CaptureAuthorityAsync(cancellationToken);
        Result approved = await approval.RequireAsync("CredentialCutover.ReviewResourceWindow", review.ResourceId.ToString(), cancellationToken: cancellationToken);
        if (approved.IsFailure) { return approved; }
        if (review.Mechanism is not ("OnlineIntrospection" or "ConsumerRevocation" or "OfflineExpiry") || review.ResidualSeconds < 0 ||
            string.IsNullOrWhiteSpace(review.EvidenceReference) || review.EvidenceReference.Length > 1000)
        {
            await approval.RecordOutcomeAsync(false, cancellationToken);
            return DomainError.Validation("CredentialCutover.InvalidResourceWindow", "Specify the external control, a nonnegative maximum residual window, and an evidence reference of at most 1000 characters.");
        }
        Result result = await store.ReviewResourceWindowAsync(review, actor.AuditActorId, cancellationToken);
        await approval.RecordOutcomeAsync(result.IsSuccess, cancellationToken);
        return result;
    }
}
