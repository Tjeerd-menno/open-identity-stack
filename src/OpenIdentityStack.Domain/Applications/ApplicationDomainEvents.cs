using SharedKernel;

namespace OpenIdentityStack.Domain.Applications;

/// <summary>
/// Domain events related to applications.
/// </summary>
public static class ApplicationDomainEvents
{
    public sealed record ApplicationCreated(
        ApplicationId ApplicationId,
        string ClientId,
        string DisplayName,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    public sealed record ApplicationMetadataUpdated(
        ApplicationId ApplicationId,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    public sealed record ApplicationOAuthConfigured(
        ApplicationId ApplicationId,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    public sealed record ApplicationDisabled(
        ApplicationId ApplicationId,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    public sealed record ApplicationEnabled(
        ApplicationId ApplicationId,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    public sealed record ApplicationDeleted(
        ApplicationId ApplicationId,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);
}
