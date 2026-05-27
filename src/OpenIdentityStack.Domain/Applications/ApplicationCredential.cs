using SharedKernel;

namespace OpenIdentityStack.Domain.Applications;

/// <summary>
/// Credential metadata for a confidential application.
/// </summary>
public sealed class ApplicationCredential : Entity<Guid>
{
    private ApplicationCredential()
    {
    }

    public ApplicationId ApplicationId { get; private set; }

    public ApplicationCredentialType Type { get; private set; }

    public string? SecretHash { get; private set; }

    public string? Thumbprint { get; private set; }

    public string? Subject { get; private set; }

    public string? Description { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    private ApplicationCredential(
        Guid id,
        ApplicationId applicationId,
        ApplicationCredentialType type,
        string? secretHash,
        string? thumbprint,
        string? subject,
        string? description,
        DateTimeOffset? expiresAt,
        DateTimeOffset createdAt) : base(id)
    {
        this.ApplicationId = applicationId;
        this.Type = type;
        this.SecretHash = secretHash;
        this.Thumbprint = thumbprint;
        this.Subject = subject;
        this.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        this.ExpiresAt = expiresAt;
        this.CreatedAt = createdAt;
        this.SetModified(createdAt);
    }

    public static Result<ApplicationCredential> CreateClientSecret(
        ApplicationId applicationId,
        string secretHash,
        string? description,
        DateTimeOffset? expiresAt,
        IDateTimeProvider dateTimeProvider)
    {
        if (string.IsNullOrWhiteSpace(secretHash))
        {
            return ApplicationErrors.SecretHashRequired;
        }

        return new ApplicationCredential(
            Guid.NewGuid(),
            applicationId,
            ApplicationCredentialType.ClientSecret,
            secretHash.Trim(),
            thumbprint: null,
            subject: null,
            description,
            expiresAt,
            dateTimeProvider.UtcNow);
    }

    public static Result<ApplicationCredential> CreateCertificate(
        ApplicationId applicationId,
        string thumbprint,
        string? subject,
        string? description,
        DateTimeOffset? expiresAt,
        IDateTimeProvider dateTimeProvider)
    {
        if (string.IsNullOrWhiteSpace(thumbprint))
        {
            return ApplicationErrors.CertificateThumbprintRequired;
        }

        return new ApplicationCredential(
            Guid.NewGuid(),
            applicationId,
            ApplicationCredentialType.X509Certificate,
            secretHash: null,
            thumbprint.Trim(),
            string.IsNullOrWhiteSpace(subject) ? null : subject.Trim(),
            description,
            expiresAt,
            dateTimeProvider.UtcNow);
    }

    public bool IsActiveAt(DateTimeOffset timestamp) =>
        this.RevokedAt is null && (this.ExpiresAt is null || this.ExpiresAt > timestamp);

    public Result Revoke(IDateTimeProvider dateTimeProvider)
    {
        if (this.RevokedAt is not null)
        {
            return ApplicationErrors.CredentialAlreadyRevoked;
        }

        this.RevokedAt = dateTimeProvider.UtcNow;
        this.SetModified(dateTimeProvider.UtcNow);
        return Result.Success();
    }

    public Result MarkUsed(IDateTimeProvider dateTimeProvider)
    {
        if (!this.IsActiveAt(dateTimeProvider.UtcNow))
        {
            return ApplicationErrors.CredentialNotActive;
        }

        this.LastUsedAt = dateTimeProvider.UtcNow;
        this.SetModified(dateTimeProvider.UtcNow);
        return Result.Success();
    }
}
