using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Audit;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Persistence;

public sealed class CredentialCutoverReadinessStore(OpenIdentityStackDbContext db,
    ICredentialCutoverResourceInventory resources, IDateTimeProvider clock) : ICredentialCutoverReadinessStore
{
    public async Task<CredentialCutoverPreflight> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        Guid epoch = await this.GetEpochAsync(cancellationToken);
        DateTimeOffset now = clock.UtcNow;
        var blockers = new List<CutoverBlocker>();
        List<IdentityCounts> identities = await db.Users.AsNoTracking().Select(user => new IdentityCounts(
            user.UpstreamIdentities.Count(link => link.AssociationEvidence != IdentityAssociationEvidence.NewAccountProvisioning || link.Issuer == null || link.Issuer.Trim() == ""),
            user.PasswordHash != null && user.PasswordHash != "", user.Status == UserStatus.Disabled,
            user.EmailVerificationEvidence.Any(e => e.WithdrawnAt == null && e.NormalizedEmail == user.NormalizedEmail),
            user.EmailVerificationEvidence.Count(e => e.ProviderId != null), user.EmailVerificationEvidence.Count(e => e.WithdrawnAt != null)))
            .ToListAsync(cancellationToken);
        int quarantined = identities.Sum(x => x.Quarantined);
        if (quarantined > 0)
        {
            blockers.Add(new("Identity.Quarantined", "Quarantined links require a separately specified proof or recovery workflow. Password configuration is only a candidate, not association proof.", quarantined));
        }

        EmergencyAccessEvidence? emergency = null;
        List<EmergencyAccessRecord> proofs = await db.EmergencyAccessEvidence.AsNoTracking()
            .Where(x => x.Epoch == epoch).OrderByDescending(x => x.RecordedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        foreach (EmergencyAccessRecord proof in proofs)
        {
            bool usable = await this.IsEmergencyAccessCurrentAsync(proof, cancellationToken);
            if (emergency is null || usable)
            {
                emergency = new(proof.Id, proof.UserId, proof.SessionId, proof.AuthenticatedAt, proof.RecordedAt, usable);
            }
            if (usable) { break; }
        }
        if (emergency?.CurrentlyUsable != true)
        {
            blockers.Add(new("Emergency.IndependentAccessRequired", "Test a fresh local-password login with a currently unrestricted emergency administrator and record that session."));
        }

        CutoverResourceInventory inventory = await resources.ReadAsync(cancellationToken);
        blockers.AddRange(inventory.Blockers);
        List<DateTime?> expiries = await db.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .Where(token => token.Type == "access_token" && (token.ExpirationDate == null || token.ExpirationDate > now.UtcDateTime))
            .Select(token => token.ExpirationDate).ToListAsync(cancellationToken);
        DateTimeOffset? latestExpiry = expiries.Any(x => x.HasValue)
            ? new DateTimeOffset(DateTime.SpecifyKind(expiries.Where(x => x.HasValue).Max()!.Value, DateTimeKind.Utc)) : null;
        List<ResourceWindowReviewRecord> reviews = await db.ResourceTokenWindowReviews.AsNoTracking()
            .Where(x => x.Epoch == epoch).OrderByDescending(x => x.ReviewedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var windows = new List<ResourceTokenWindow>();
        foreach (CutoverProtectedResource resource in inventory.BusinessResources.OrderBy(x => x.Id))
        {
            ResourceWindowReviewRecord? review = reviews.FirstOrDefault(x => x.ResourceId == resource.Id && x.ResourceRevision == resource.Revision);
            bool reviewed = review is not null;
            if (review?.Mechanism == "OfflineExpiry")
            {
                // Token rows do not retain a resource index. Use the conservative bound across all OP access tokens.
                reviewed = !expiries.Any(x => x is null) && (latestExpiry is null || latestExpiry <= now.AddSeconds(review.ResidualSeconds));
            }
            if (!reviewed)
            {
                blockers.Add(new("Resource.TokenWindowUnresolved", $"Review the external token controls and remaining window for resource {resource.Id}."));
            }
            windows.Add(new(resource.Id, resource.DisplayName, resource.Audience, resource.Scope, resource.Revision,
                review?.Mechanism, review?.ResidualSeconds, review?.EvidenceReference, review?.ReviewedAt, reviewed));
        }
        var summary = new CutoverIdentityInventory(quarantined, identities.Count(x => x.Quarantined > 0),
            identities.Count(x => x.Quarantined > 0 && !x.Password), identities.Count(x => x.Quarantined > 0 && x.Password),
            identities.Count(x => x.Disabled), identities.Count(x => x.Verified), identities.Sum(x => x.ProviderEvidence), identities.Sum(x => x.WithdrawnEvidence));
        return new(epoch, now, blockers, emergency, summary, inventory.AdministrativeClients, windows, expiries.Count, latestExpiry);
    }

    public async Task<Result<EmergencyAccessEvidence>> RecordEmergencyAccessAsync(AdministrativeActor actor, CancellationToken cancellationToken = default)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        Guid epoch = await this.GetEpochAsync(cancellationToken);
        if (!actor.IsHuman || actor.LocalPasswordSessionId is not Guid session || actor.AuthenticatedAt is not DateTimeOffset authenticatedAt || actor.CredentialEpoch != epoch)
        {
            return DomainError.Forbidden("CredentialCutover.IndependentLoginRequired", "A fresh local password login bound to the current credential boundary is required.");
        }
        var proof = new EmergencyAccessRecord { Id = Guid.NewGuid(), UserId = actor.UserId.Value, SessionId = session, Epoch = epoch, AuthenticatedAt = authenticatedAt, RecordedAt = clock.UtcNow };
        if (!await this.IsEmergencyAccessCurrentAsync(proof, cancellationToken))
        {
            return DomainError.Forbidden("CredentialCutover.EmergencyAccessUnavailable", "Test current independent emergency access before continuing.");
        }
        db.EmergencyAccessEvidence.Add(proof);
        db.AuditLogEntries.Add(new AuditLogEntry { UserId = actor.UserId.Value.ToString(), Action = "CredentialCutover.EmergencyAccessTested", EntityType = "CredentialBoundary", EntityId = epoch.ToString(), Timestamp = clock.UtcNow,
            Details = "Fresh local password authentication and a live session established current unrestricted emergency access.", AfterState = JsonSerializer.Serialize(proof) });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new EmergencyAccessEvidence(proof.Id, proof.UserId, proof.SessionId, proof.AuthenticatedAt, proof.RecordedAt, true);
    }

    public async Task<Result> ReviewResourceWindowAsync(ResourceWindowReview review, string actorId, CancellationToken cancellationToken = default)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        Guid epoch = await this.GetEpochAsync(cancellationToken);
        CutoverResourceInventory inventory = await resources.ReadAsync(cancellationToken);
        CutoverProtectedResource? resource = inventory.BusinessResources.SingleOrDefault(x => x.Id == review.ResourceId);
        if (resource is null) { return DomainError.NotFound("CredentialCutover.ResourceNotFound", "The business resource was not found."); }
        var record = new ResourceWindowReviewRecord { Id = Guid.NewGuid(), ResourceId = resource.Id, ResourceRevision = resource.Revision, Epoch = epoch,
            Mechanism = review.Mechanism, ResidualSeconds = review.ResidualSeconds, EvidenceReference = review.EvidenceReference.Trim(), ReviewedAt = clock.UtcNow };
        db.ResourceTokenWindowReviews.Add(record);
        db.AuditLogEntries.Add(new AuditLogEntry { UserId = actorId, Action = "CredentialCutover.ResourceWindowReviewed", EntityType = "ProtectedResource", EntityId = resource.Id.ToString(), Timestamp = clock.UtcNow,
            Details = "Operator recorded external control evidence and accepted its residual token window; external enforcement is not automatically verified.", AfterState = JsonSerializer.Serialize(record) });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private Task<Guid> GetEpochAsync(CancellationToken cancellationToken) =>
        db.Set<CredentialBoundaryState>().Select(x => x.Epoch).SingleAsync(cancellationToken);

    private async Task<bool> IsEmergencyAccessCurrentAsync(EmergencyAccessRecord proof, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.UtcNow;
        if (proof.AuthenticatedAt > now || now - proof.AuthenticatedAt > TimeSpan.FromMinutes(5)) { return false; }
        var userId = new UserId(proof.UserId);
        User? user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null || !user.CanAuthenticate() || !user.HasPassword()) { return false; }
        UserSession? session = await db.UserSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == new SessionId(proof.SessionId), cancellationToken);
        if (session is null || session.UserId != userId || session.Status != SessionStatus.Active || session.IsExpired(clock) ||
            session.CreatedAt < proof.AuthenticatedAt || session.CreatedAt > proof.AuthenticatedAt.AddSeconds(5)) { return false; }
        bool localLoginUnavailable = await db.AuthenticationSettings.AsNoTracking()
            .AnyAsync(x => x.DefaultProviderId != "local" && !x.LocalFallbackEnabled, cancellationToken);
        return !localLoginUnavailable && await this.HasCurrentUnrestrictedAuthorityAsync(userId, cancellationToken);
    }

    private async Task<bool> HasCurrentUnrestrictedAuthorityAsync(UserId userId, CancellationToken cancellationToken)
    {
        List<RoleId> direct = await db.RoleAssignments.Where(x => x.UserId == userId).Select(x => x.RoleId).ToListAsync(cancellationToken);
        List<string> mapped = await db.Groups.Where(x => x.Memberships.Any(m => m.UserId == userId))
            .SelectMany(x => x.Mappings.Where(m => m.Type == OpenIdentityStack.Domain.Groups.MappingType.Role).Select(m => m.Target)).ToListAsync(cancellationToken);
        List<Role> active = await db.Roles.AsNoTracking().Where(x => x.IsActive).ToListAsync(cancellationToken);
        return active.Any(role => role.Permissions.Contains("*", StringComparer.Ordinal) &&
            (direct.Contains(role.Id) || mapped.Any(target => Guid.TryParse(target, out Guid id)
                ? role.Id.Value == id : string.Equals(role.Name, target, StringComparison.OrdinalIgnoreCase))));
    }

    private sealed record IdentityCounts(int Quarantined, bool Password, bool Disabled, bool Verified, int ProviderEvidence, int WithdrawnEvidence);
}
