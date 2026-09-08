namespace OpenIdentityStack.Domain.Users;

/// <summary>Evidence for a particular email address, independent of account activation.</summary>
public sealed class EmailVerificationEvidence
{
    private EmailVerificationEvidence() { }

    internal EmailVerificationEvidence(string email, Guid? providerId, string? issuer, DateTimeOffset verifiedAt)
    {
        this.Id = Guid.NewGuid();
        this.NormalizedEmail = email.Trim().ToUpperInvariant();
        this.ProviderId = providerId;
        this.Issuer = issuer;
        this.VerifiedAt = verifiedAt;
    }

    public Guid Id { get; private set; }
    public string NormalizedEmail { get; private set; } = string.Empty;
    public Guid? ProviderId { get; private set; }
    public string? Issuer { get; private set; }
    public DateTimeOffset VerifiedAt { get; private set; }
    public DateTimeOffset? WithdrawnAt { get; private set; }

    internal void Withdraw(DateTimeOffset withdrawnAt) => this.WithdrawnAt ??= withdrawnAt;
}
