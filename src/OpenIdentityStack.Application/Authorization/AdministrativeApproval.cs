using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Queries;
using SharedKernel;

namespace OpenIdentityStack.Application.Authorization;

public sealed class AdministrativeApproval(
    IAdministrativeActorContext actorContext,
    IUserRepository users,
    IGetUserEffectiveRolesQueryHandler roles,
    IDateTimeProvider clock,
    IAdministrativeApprovalAudit audit,
    IAdministrativeAuthoritySnapshot authoritySnapshot) : IAdministrativeApproval
{
    private readonly List<ApprovalIntent> pending = [];

    public Task CaptureAuthorityAsync(CancellationToken cancellationToken = default) => authoritySnapshot.CaptureAsync(cancellationToken);

    public async Task<Result> RequireForUserAccessAsync(UserId userId, string operation, CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<RoleDto>> targetRoles = await roles.HandleAsync(userId, cancellationToken);
        if (targetRoles.IsFailure) { return targetRoles.Error; }
        return targetRoles.Value.Any(role => UnrestrictedGrantPolicy.IncludesAllPermissions(role.Permissions))
            ? await this.RequireAsync(operation, userId.Value.ToString(), cancellationToken: cancellationToken)
            : Result.Success();
    }

    public async Task<Result> RequireAsync(string operation, string targetId, bool acknowledged = false, CancellationToken cancellationToken = default)
    {
        AdministrativeActor? actor = actorContext.Current;
        if (actor is null || !actor.IsHuman)
        {
            return DomainError.Forbidden("AdministrativeApproval.HumanRequired", "A human administrator must approve this operation.");
        }

        OpenIdentityStack.Domain.Users.User? user = await users.GetByIdAsync(actor.UserId, cancellationToken);
        if (user is null || !user.CanAuthenticate())
        {
            return DomainError.Forbidden("AdministrativeApproval.AuthorityRequired", "Current unrestricted administrative authority is required.");
        }

        Result<IReadOnlyList<RoleDto>> effectiveRoles = await roles.HandleAsync(actor.UserId, cancellationToken);
        if (effectiveRoles.IsFailure || !effectiveRoles.Value.Any(role => role.IsActive && role.Permissions.Contains("*", StringComparer.Ordinal)))
        {
            return DomainError.Forbidden("AdministrativeApproval.AuthorityRequired", "Current unrestricted administrative authority is required.");
        }

        DateTimeOffset now = clock.UtcNow;
        if (actor.AuthenticatedAt is not { } authenticatedAt || authenticatedAt > now || now - authenticatedAt > TimeSpan.FromMinutes(5))
        {
            return DomainError.Forbidden("AdministrativeApproval.ReauthenticationRequired", "Sign in again before approving this operation.");
        }

        if (!acknowledged && !actor.Acknowledged)
        {
            return DomainError.Forbidden("AdministrativeApproval.AcknowledgementRequired", "Acknowledge that unrestricted access includes current and future platform permissions.");
        }

        await audit.LogAsync(actor.UserId.Value.ToString(), "AdministrativeApproval.IntentApproved", operation,
            AuditEntityId(targetId), $"Fresh human approval of unrestricted authority; mutation not yet committed. Target: {targetId}", cancellationToken);
        this.pending.Add(new ApprovalIntent(actor.UserId.Value.ToString(), operation, targetId));
        return Result.Success();
    }

    public async Task RecordOutcomeAsync(bool succeeded, CancellationToken cancellationToken = default)
    {
        foreach (ApprovalIntent entry in this.pending)
        {
            entry.HasSucceeded |= succeeded;
            await audit.LogAsync(entry.Actor, entry.HasSucceeded ? "AdministrativeApproval.MutationSucceeded" : "AdministrativeApproval.MutationNotConfirmed",
                entry.Operation, AuditEntityId(entry.Target),
                (entry.HasSucceeded ? "The approved mutation completed." : "No successful mutation completion was recorded; inspect persisted state before retrying.") + $" Target: {entry.Target}", cancellationToken);
        }
        this.pending.Clear();
    }

    // Composite group/role targets can exceed the audit index's 128-character key;
    // details retain the complete target for investigation.
    private static string AuditEntityId(string target) => target.Length <= 128 ? target : target[..128];

    private sealed class ApprovalIntent(string actor, string operation, string target)
    {
        public string Actor { get; } = actor;
        public string Operation { get; } = operation;
        public string Target { get; } = target;
        public bool HasSucceeded { get; set; }
    }
}
