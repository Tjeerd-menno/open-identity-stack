using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.ServiceAccounts;

/// <summary>
/// Domain events related to service accounts.
/// </summary>
public static class ServiceAccountDomainEvents
{
    /// <summary>
    /// Raised when a new service account is created.
    /// </summary>
    public sealed record ServiceAccountCreated(
        ServiceAccountId ServiceAccountId,
        string ClientId,
        string DisplayName,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    /// <summary>
    /// Raised when a service account is disabled.
    /// </summary>
    public sealed record ServiceAccountDisabled(
        ServiceAccountId ServiceAccountId,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    /// <summary>
    /// Raised when a service account is enabled.
    /// </summary>
    public sealed record ServiceAccountEnabled(
        ServiceAccountId ServiceAccountId,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    /// <summary>
    /// Raised when a credential is added to a service account.
    /// </summary>
    public sealed record CredentialAdded(
        ServiceAccountId ServiceAccountId,
        Guid CredentialId,
        string? Description,
        DateTimeOffset? ExpiresAt,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    /// <summary>
    /// Raised when a credential is revoked from a service account.
    /// </summary>
    public sealed record CredentialRevoked(
        ServiceAccountId ServiceAccountId,
        Guid CredentialId,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    /// <summary>
    /// Raised when a certificate is added to a service account.
    /// </summary>
    public sealed record CertificateAdded(
        ServiceAccountId ServiceAccountId,
        Guid CertificateId,
        string Thumbprint,
        string Subject,
        DateTimeOffset ExpiresAt,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    /// <summary>
    /// Raised when a certificate is revoked from a service account.
    /// </summary>
    public sealed record CertificateRevoked(
        ServiceAccountId ServiceAccountId,
        Guid CertificateId,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    /// <summary>
    /// Raised when a service account's configuration is updated.
    /// </summary>
    public sealed record ServiceAccountUpdated(
        ServiceAccountId ServiceAccountId,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);
}
