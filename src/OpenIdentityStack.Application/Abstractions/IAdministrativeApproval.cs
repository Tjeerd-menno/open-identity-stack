using SharedKernel;

namespace OpenIdentityStack.Application.Abstractions;

public sealed record AdministrativeActor(UserId UserId, DateTimeOffset? AuthenticatedAt, bool IsHuman, bool Acknowledged);

public interface IAdministrativeActorContext
{
    AdministrativeActor? Current { get; }
    string AuditActorId => this.Current?.UserId.Value.ToString() ?? "system";
}

/// <summary>Non-HTTP hosts have no authenticated human approval context.</summary>
public sealed class UnauthenticatedAdministrativeActorContext : IAdministrativeActorContext
{
    public AdministrativeActor? Current => null;
}

public interface IAdministrativeApproval
{
    Task CaptureAuthorityAsync(CancellationToken cancellationToken = default);
    Task<Result> RequireAsync(string operation, string targetId, bool acknowledged = false, CancellationToken cancellationToken = default);
    Task<Result> RequireForUserAccessAsync(UserId userId, string operation, CancellationToken cancellationToken = default);
    Task RecordOutcomeAsync(bool succeeded, CancellationToken cancellationToken = default);
}

/// <summary>Captures authority read dependencies before an access-changing use case reads its entities.</summary>
public interface IAdministrativeAuthoritySnapshot
{
    /// <summary>Returns the revision captured for this scope. A later capture in the same scope returns that same value.</summary>
    Task<long> CaptureAsync(CancellationToken cancellationToken = default);
}

/// <summary>Persists approval audit independently of the mutation's tracked state.</summary>
public interface IAdministrativeApprovalAudit : IAuditLog;
