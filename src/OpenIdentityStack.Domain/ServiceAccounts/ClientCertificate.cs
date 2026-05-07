using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Domain.ServiceAccounts;

/// <summary>
/// Represents a client certificate for a service account.
/// </summary>
public sealed class ClientCertificate : IEquatable<ClientCertificate>
{
    /// <summary>
    /// Gets the unique identifier for this certificate.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the ID of the parent service account.
    /// </summary>
    public ServiceAccountId ServiceAccountId { get; private set; }

    /// <summary>
    /// Gets the SHA-256 thumbprint of the certificate.
    /// </summary>
    public string Thumbprint { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the subject of the certificate.
    /// </summary>
    public string Subject { get; private set; } = string.Empty;

    /// <summary>
    /// Gets when the certificate expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// Gets when this certificate was registered.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Gets whether this certificate has been revoked.
    /// </summary>
    public bool IsRevoked { get; private set; }

    // EF Core constructor
    private ClientCertificate() { }

    private ClientCertificate(
        Guid id,
        ServiceAccountId serviceAccountId,
        string thumbprint,
        string subject,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        bool isRevoked)
    {
        this.Id = id;
        this.ServiceAccountId = serviceAccountId;
        this.Thumbprint = thumbprint;
        this.Subject = subject;
        this.ExpiresAt = expiresAt;
        this.CreatedAt = createdAt;
        this.IsRevoked = isRevoked;
    }

    /// <summary>
    /// Creates a new client certificate.
    /// </summary>
    public static ClientCertificate Create(
        ServiceAccountId serviceAccountId,
        string thumbprint,
        string subject,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        return new ClientCertificate(
            Guid.NewGuid(),
            serviceAccountId,
            thumbprint,
            subject,
            expiresAt,
            createdAt,
            isRevoked: false);
    }

    /// <summary>
    /// Checks if this certificate is expired at the given time.
    /// </summary>
    public bool IsExpired(DateTimeOffset asOf)
    {
        return asOf >= this.ExpiresAt;
    }

    /// <summary>
    /// Checks if this certificate is valid (not expired and not revoked).
    /// </summary>
    public bool IsValid(DateTimeOffset asOf)
    {
        return !this.IsRevoked && !this.IsExpired(asOf);
    }

    /// <summary>
    /// Returns a new certificate with IsRevoked set to true.
    /// </summary>
    public ClientCertificate Revoke()
    {
        return new ClientCertificate(
            this.Id,
            this.ServiceAccountId,
            this.Thumbprint,
            this.Subject,
            this.ExpiresAt,
            this.CreatedAt,
            isRevoked: true);
    }

    /// <summary>
    /// Internal method to mark as revoked (for aggregate use).
    /// </summary>
    internal void MarkRevoked()
    {
        this.IsRevoked = true;
    }

    public bool Equals(ClientCertificate? other)
    {
        if (other is null)
        {
            return false;
        }

        return this.Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return this.Equals(obj as ClientCertificate);
    }

    public override int GetHashCode()
    {
        return this.Id.GetHashCode();
    }
}
