using SharedKernel;
namespace SharedKernel;

/// <summary>
/// Base record for domain events.
/// Domain events represent something that happened in the domain that domain experts care about.
/// They are immutable and carry all information about what happened.
/// </summary>
/// <param name="OccurredAt">The timestamp when this event occurred.</param>
public abstract record DomainEvent(DateTimeOffset OccurredAt)
{
    /// <summary>
    /// Gets the unique identifier for this event instance.
    /// </summary>
    public Guid EventId { get; } = Guid.NewGuid();
}
