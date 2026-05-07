using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Groups;

/// <summary>
/// Group domain events.
/// </summary>
public static class GroupDomainEvents
{
    /// <summary>
    /// Raised when a new group is created.
    /// </summary>
    public sealed record GroupCreated(
        GroupId GroupId,
        string Name,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    /// <summary>
    /// Raised when a group is updated.
    /// </summary>
    public sealed record GroupUpdated(
        GroupId GroupId,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    /// <summary>
    /// Raised when a group is deleted.
    /// </summary>
    public sealed record GroupDeleted(
        GroupId GroupId,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);
}
