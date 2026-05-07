using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Domain.ServiceAccounts;

/// <summary>
/// Represents a client credential (secret) for a service account.
/// </summary>
public sealed class ClientCredential : IEquatable<ClientCredential>
{
    /// <summary>
    /// Gets the unique identifier for this credential.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the ID of the parent service account.
    /// </summary>
    public ServiceAccountId ServiceAccountId { get; private set; }

    /// <summary>
    /// Gets the hashed client secret.
    /// </summary>
    public string SecretHash { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the description/purpose of this credential.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Gets the expiration time of this credential, if any.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>
    /// Gets when this credential was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Gets when this credential was last used, if ever.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; private set; }

    /// <summary>
    /// Gets whether this credential has been revoked.
    /// </summary>
    public bool IsRevoked { get; private set; }

    // EF Core constructor
    private ClientCredential() { }

    private ClientCredential(
        Guid id,
        ServiceAccountId serviceAccountId,
        string secretHash,
        string? description,
        DateTimeOffset? expiresAt,
        DateTimeOffset createdAt,
        DateTimeOffset? lastUsedAt,
        bool isRevoked)
    {
        this.Id = id;
        this.ServiceAccountId = serviceAccountId;
        this.SecretHash = secretHash;
        this.Description = description;
        this.ExpiresAt = expiresAt;
        this.CreatedAt = createdAt;
        this.LastUsedAt = lastUsedAt;
        this.IsRevoked = isRevoked;
    }

    /// <summary>
    /// Creates a new client credential.
    /// </summary>
    public static ClientCredential Create(
        ServiceAccountId serviceAccountId,
        string secretHash,
        string? description,
        DateTimeOffset? expiresAt,
        DateTimeOffset createdAt)
    {
        return new ClientCredential(
            Guid.NewGuid(),
            serviceAccountId,
            secretHash,
            description,
            expiresAt,
            createdAt,
            lastUsedAt: null,
            isRevoked: false);
    }

    /// <summary>
    /// Creates a credential with specific values (for testing).
    /// </summary>
    public static ClientCredential CreateForTesting(
        Guid id,
        ServiceAccountId serviceAccountId,
        string secretHash,
        string? description,
        DateTimeOffset? expiresAt,
        DateTimeOffset createdAt,
        DateTimeOffset? lastUsedAt,
        bool isRevoked)
    {
        return new ClientCredential(
            id,
            serviceAccountId,
            secretHash,
            description,
            expiresAt,
            createdAt,
            lastUsedAt,
            isRevoked);
    }

    /// <summary>
    /// Checks if this credential is expired at the given time.
    /// </summary>
    public bool IsExpired(DateTimeOffset asOf)
    {
        return this.ExpiresAt.HasValue && asOf >= this.ExpiresAt.Value;
    }

    /// <summary>
    /// Checks if this credential is valid (not expired and not revoked).
    /// </summary>
    public bool IsValid(DateTimeOffset asOf)
    {
        return !this.IsRevoked && !this.IsExpired(asOf);
    }

    /// <summary>
    /// Returns a new credential with IsRevoked set to true.
    /// </summary>
    public ClientCredential Revoke()
    {
        return new ClientCredential(
            this.Id,
            this.ServiceAccountId,
            this.SecretHash,
            this.Description,
            this.ExpiresAt,
            this.CreatedAt,
            this.LastUsedAt,
            isRevoked: true);
    }

    /// <summary>
    /// Returns a new credential with LastUsedAt updated.
    /// </summary>
    public ClientCredential RecordUsage(DateTimeOffset usedAt)
    {
        return new ClientCredential(
            this.Id,
            this.ServiceAccountId,
            this.SecretHash,
            this.Description,
            this.ExpiresAt,
            this.CreatedAt,
            usedAt,
            this.IsRevoked);
    }

    /// <summary>
    /// Internal method to mark as revoked (for aggregate use).
    /// </summary>
    internal void MarkRevoked()
    {
        this.IsRevoked = true;
    }

    /// <summary>
    /// Internal method to update last used time (for aggregate use).
    /// </summary>
    internal void UpdateLastUsed(DateTimeOffset usedAt)
    {
        this.LastUsedAt = usedAt;
    }

    public bool Equals(ClientCredential? other)
    {
        if (other is null)
        {
            return false;
        }

        return this.Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return this.Equals(obj as ClientCredential);
    }

    public override int GetHashCode()
    {
        return this.Id.GetHashCode();
    }
}
