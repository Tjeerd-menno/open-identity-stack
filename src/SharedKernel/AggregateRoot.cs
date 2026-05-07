using SharedKernel;
namespace SharedKernel;

/// <summary>
/// Base class for aggregate roots.
/// Aggregate roots are entities that serve as the entry point to an aggregate.
/// They maintain domain events that can be published after persistence.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<DomainEvent> domainEvents = [];

    /// <summary>
    /// Gets the collection of domain events raised by this aggregate.
    /// </summary>
    public IReadOnlyCollection<DomainEvent> DomainEvents => this.domainEvents.AsReadOnly();

    protected AggregateRoot() { }

    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>
    /// Gets the domain events as a list for inspection.
    /// </summary>
    public IReadOnlyList<DomainEvent> GetDomainEvents() => this.domainEvents.AsReadOnly();

    /// <summary>
    /// Raises a domain event to be published after the aggregate is persisted.
    /// </summary>
    /// <param name="domainEvent">The domain event to raise.</param>
    protected void RaiseDomainEvent(DomainEvent domainEvent)
    {
        this.domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears all domain events. Should be called after events have been published.
    /// </summary>
    public void ClearDomainEvents()
    {
        this.domainEvents.Clear();
    }
}
