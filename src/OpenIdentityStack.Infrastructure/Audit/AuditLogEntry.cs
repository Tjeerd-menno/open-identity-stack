using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Audit;

/// <summary>
/// Persisted audit log record for administrative actions.
/// </summary>
public sealed class AuditLogEntry
{
    private string userId = string.Empty;
    public Guid Id { get; init; } = Guid.NewGuid();

    public string UserId { get => this.userId; init => this.userId = AuditActorIdentifier.Normalize(value); }

    public string Action { get; init; } = string.Empty;

    public string EntityType { get; init; } = string.Empty;

    public string EntityId { get; init; } = string.Empty;

    public string? Details { get; init; }

    public string? BeforeState { get; init; }

    public string? AfterState { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}
