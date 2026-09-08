
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;

using SharedKernel;
namespace OpenIdentityStack.Domain.Users;
/// <summary>
/// Represents a linked identity from an upstream provider.
/// </summary>
public sealed class UpstreamIdentity : IEquatable<UpstreamIdentity>
{
    private UpstreamIdentity(
        UpstreamProviderId providerId,
        string providerName,
        string subjectId,
        string? email, string? issuer = null)
    {
        this.ProviderId = providerId;
        this.ProviderName = providerName;
        this.SubjectId = subjectId;
        this.Email = email;
        this.Issuer = issuer;
        this.LinkedAt = DateTimeOffset.UtcNow;
    }

    // EF Core constructor
    private UpstreamIdentity()
    {
        this.ProviderName = string.Empty;
        this.SubjectId = string.Empty;
    }

    /// <summary>
    /// Gets the upstream provider ID.
    /// </summary>
    public UpstreamProviderId ProviderId { get; private set; }

    /// <summary>
    /// Gets the provider name (e.g., "azure-ad", "google").
    /// </summary>
    public string ProviderName { get; private set; }

    /// <summary>
    /// Gets the subject ID from the upstream provider.
    /// </summary>
    public string SubjectId { get; private set; }

    /// <summary>Gets the exact validated issuer; null means no trusted issuer evidence.</summary>
    public string? Issuer { get; private set; }

    /// <summary>Gets the independent evidence establishing this association.</summary>
    public IdentityAssociationEvidence AssociationEvidence { get; private set; }

    /// <summary>Gets whether authentication is prohibited until independent association evidence exists.</summary>
    public bool IsQuarantined => this.AssociationEvidence != IdentityAssociationEvidence.NewAccountProvisioning || string.IsNullOrWhiteSpace(this.Issuer);

    /// <summary>Creates an identity for a newly provisioned account, never an existing account association.</summary>
    internal static Result<UpstreamIdentity> CreateForNewAccount(UpstreamProviderId providerId, string providerName, string subjectId, string? email, string issuer)
    {
        Result<UpstreamIdentity> result = Create(providerId, providerName, subjectId, email, issuer);
        if (result.IsSuccess && !string.IsNullOrWhiteSpace(issuer))
        {
            result.Value.AssociationEvidence = IdentityAssociationEvidence.NewAccountProvisioning;
        }
        return result;
    }

    /// <summary>
    /// Gets the email address from the upstream provider (may differ from local).
    /// </summary>
    public string? Email { get; private set; }

    /// <summary>
    /// Gets the timestamp when the identity was linked.
    /// </summary>
    public DateTimeOffset LinkedAt { get; private set; }

    /// <summary>
    /// Gets the timestamp of the last login via this identity.
    /// </summary>
    public DateTimeOffset? LastLoginAt { get; private set; }

    /// <summary>
    /// Creates a new upstream identity.
    /// </summary>
    public static Result<UpstreamIdentity> Create(
        UpstreamProviderId providerId,
        string providerName,
        string subjectId,
        string? email, string? issuer = null)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return UpstreamIdentityErrors.ProviderNameRequired;
        }

        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return UpstreamIdentityErrors.SubjectIdRequired;
        }

        return new UpstreamIdentity(
            providerId,
            providerName.Trim().ToLowerInvariant(),
            subjectId,
            string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant(), issuer);
    }

    /// <summary>
    /// Checks if this identity matches the given provider and subject.
    /// </summary>
    public bool Matches(UpstreamProviderId providerId, string subjectId)
    {
        return this.ProviderId == providerId && this.SubjectId == subjectId;
    }

    /// <summary>
    /// Updates the email from the upstream provider.
    /// </summary>
    public Result<UpstreamIdentity> UpdateEmail(string? email)
    {
        return new UpstreamIdentity(
            this.ProviderId,
            this.ProviderName,
            this.SubjectId,
            string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant(), this.Issuer)
        {
            AssociationEvidence = this.AssociationEvidence,
            LinkedAt = this.LinkedAt,
            LastLoginAt = this.LastLoginAt,
        };
    }

    /// <summary>
    /// Records a login via this identity.
    /// </summary>
    public void RecordLogin()
    {
        this.LastLoginAt = DateTimeOffset.UtcNow;
    }

    /// <inheritdoc />
    public bool Equals(UpstreamIdentity? other)
    {
        if (other is null)
        {
            return false;
        }

        return this.ProviderId == other.ProviderId && this.SubjectId == other.SubjectId;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => this.Equals(obj as UpstreamIdentity);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(this.ProviderId, this.SubjectId);
}

/// <summary>
/// Upstream identity related errors.
/// </summary>
public static class UpstreamIdentityErrors
{
    public static readonly DomainError QuarantineRetentionRequired =
        DomainError.Forbidden("UpstreamIdentity.QuarantineRetentionRequired", "Quarantined identity evidence must be retained until a proof-based disposition is available.");
    public static readonly DomainError ProviderNameRequired =
        DomainError.Validation("UpstreamIdentity.ProviderNameRequired", "Provider name is required.");

    public static readonly DomainError SubjectIdRequired =
        DomainError.Validation("UpstreamIdentity.SubjectIdRequired", "Subject ID is required.");

    public static readonly DomainError AlreadyLinked =
        DomainError.Conflict("UpstreamIdentity.AlreadyLinked", "This upstream identity is already linked to a user.");

    public static readonly DomainError NotLinked =
        DomainError.NotFound("UpstreamIdentity.NotLinked", "No upstream identity found for this provider.");
}

/// <summary>Evidence that a federated identity legitimately belongs to its local account.</summary>
public enum IdentityAssociationEvidence
{
    Unknown = 0,
    NewAccountProvisioning = 1,
}
