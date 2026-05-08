using SharedKernel;
namespace SharedKernel;

/// <summary>
/// Base class for all domain entities.
/// Entities are identified by their unique identifier and implement equality based on that identifier.
/// </summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    /// <summary>
    /// Gets the unique identifier for this entity.
    /// </summary>
    public TId Id { get; protected init; } = default!;

    /// <summary>
    /// Gets the date and time when this entity was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; protected set; }

    /// <summary>
    /// Gets or sets the date and time when this entity was last modified.
    /// </summary>
    public DateTimeOffset? ModifiedAt { get; protected set; }

    protected Entity() { }

    protected Entity(TId id)
    {
        this.Id = id;
    }

    /// <summary>
    /// Updates the modification timestamp.
    /// </summary>
    protected void SetModified(DateTimeOffset timestamp)
    {
        this.ModifiedAt = timestamp;
    }

    public override bool Equals(object? obj)
    {
        return obj is Entity<TId> entity && this.Equals(entity);
    }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (this.GetType() != other.GetType())
        {
            return false;
        }

        if (EqualityComparer<TId>.Default.Equals(this.Id, default) ||
            EqualityComparer<TId>.Default.Equals(other.Id, default))
        {
            return false;
        }
        return EqualityComparer<TId>.Default.Equals(this.Id, other.Id);
    }

    public override int GetHashCode()
    {
        return EqualityComparer<TId>.Default.Equals(this.Id, default)
            ? 0
            : EqualityComparer<TId>.Default.GetHashCode(this.Id);
    }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
    {
        return !Equals(left, right);
    }
}
