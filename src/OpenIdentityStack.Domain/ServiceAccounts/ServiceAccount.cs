using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.ServiceAccounts;

/// <summary>
/// Represents a service account (machine client) that can authenticate via client credentials.
/// </summary>
public sealed class ServiceAccount : AggregateRoot<ServiceAccountId>
{
    private static readonly HashSet<string> validGrantTypes =
    [
        "client_credentials",
        "authorization_code",
        "refresh_token",
        "urn:ietf:params:oauth:grant-type:device_code"
    ];

    private readonly List<ClientCredential> credentials = [];
    private readonly List<ClientCertificate> certificates = [];

    /// <summary>
    /// Gets the OAuth2 client_id for this service account.
    /// </summary>
    public string ClientId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the display name for this service account.
    /// </summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the current status of the service account.
    /// </summary>
    public ServiceAccountStatus Status { get; private set; }

    /// <summary>
    /// Gets the allowed OAuth2 scopes for this service account.
    /// </summary>
    public IReadOnlyList<string> AllowedScopes { get; private set; } = [];

    /// <summary>
    /// Gets the allowed OAuth2 grant types for this service account.
    /// </summary>
    public IReadOnlyList<string> AllowedGrantTypes { get; private set; } = [];

    /// <summary>
    /// Gets the credentials (client secrets) for this service account.
    /// </summary>
    public IReadOnlyList<ClientCredential> Credentials => this.credentials.AsReadOnly();

    /// <summary>
    /// Gets the certificates for this service account.
    /// </summary>
    public IReadOnlyList<ClientCertificate> Certificates => this.certificates.AsReadOnly();

    // EF Core constructor
    private ServiceAccount() : base() { }

    private ServiceAccount(
        ServiceAccountId id,
        string clientId,
        string displayName,
        IReadOnlyList<string> allowedScopes,
        IReadOnlyList<string> allowedGrantTypes,
        DateTimeOffset createdAt) : base(id)
    {
        this.ClientId = clientId;
        this.DisplayName = displayName;
        this.Status = ServiceAccountStatus.Active;
        this.AllowedScopes = allowedScopes;
        this.AllowedGrantTypes = allowedGrantTypes;
        this.CreatedAt = createdAt;
        this.SetModified(createdAt);
    }

    /// <summary>
    /// Creates a new service account.
    /// </summary>
    public static Result<ServiceAccount> Create(
        string clientId,
        string displayName,
        IReadOnlyList<string> allowedScopes,
        IReadOnlyList<string> allowedGrantTypes,
        IDateTimeProvider dateTimeProvider)
    {
        Result validationResult = ValidateInput(clientId, displayName, allowedScopes, allowedGrantTypes);
        if (validationResult.IsFailure)
        {
            return validationResult.Error;
        }

        var account = new ServiceAccount(
            ServiceAccountId.Create(),
            clientId.Trim(),
            displayName.Trim(),
            allowedScopes,
            allowedGrantTypes,
            dateTimeProvider.UtcNow);

        account.RaiseDomainEvent(new ServiceAccountDomainEvents.ServiceAccountCreated(
            account.Id,
            account.ClientId,
            account.DisplayName,
            dateTimeProvider.UtcNow));

        return account;
    }

    private static Result ValidateInput(
        string clientId,
        string displayName,
        IReadOnlyList<string> allowedScopes,
        IReadOnlyList<string> allowedGrantTypes)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return ServiceAccountErrors.ClientIdRequired;
        }

        if (clientId.Length > 128)
        {
            return ServiceAccountErrors.ClientIdTooLong;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return ServiceAccountErrors.DisplayNameRequired;
        }

        if (displayName.Length > 256)
        {
            return ServiceAccountErrors.DisplayNameTooLong;
        }

        if (allowedScopes.Count == 0)
        {
            return ServiceAccountErrors.ScopesRequired;
        }

        if (allowedGrantTypes.Count == 0)
        {
            return ServiceAccountErrors.GrantTypesRequired;
        }

        foreach (string grantType in allowedGrantTypes)
        {
            if (!validGrantTypes.Contains(grantType))
            {
                return ServiceAccountErrors.InvalidGrantType(grantType);
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Disables the service account.
    /// </summary>
    public Result Disable(IDateTimeProvider dateTimeProvider)
    {
        if (this.Status == ServiceAccountStatus.Disabled)
        {
            return ServiceAccountErrors.AlreadyDisabled;
        }

        this.Status = ServiceAccountStatus.Disabled;
        this.SetModified(dateTimeProvider.UtcNow);

        this.RaiseDomainEvent(new ServiceAccountDomainEvents.ServiceAccountDisabled(
            this.Id,
            dateTimeProvider.UtcNow));

        return Result.Success();
    }

    /// <summary>
    /// Enables the service account.
    /// </summary>
    public Result Enable(IDateTimeProvider dateTimeProvider)
    {
        if (this.Status == ServiceAccountStatus.Active)
        {
            return ServiceAccountErrors.AlreadyActive;
        }

        this.Status = ServiceAccountStatus.Active;
        this.SetModified(dateTimeProvider.UtcNow);

        this.RaiseDomainEvent(new ServiceAccountDomainEvents.ServiceAccountEnabled(
            this.Id,
            dateTimeProvider.UtcNow));

        return Result.Success();
    }

    /// <summary>
    /// Updates the display name.
    /// </summary>
    public Result UpdateDisplayName(string newDisplayName, IDateTimeProvider dateTimeProvider)
    {
        if (string.IsNullOrWhiteSpace(newDisplayName))
        {
            return ServiceAccountErrors.DisplayNameRequired;
        }

        if (newDisplayName.Length > 256)
        {
            return ServiceAccountErrors.DisplayNameTooLong;
        }

        this.DisplayName = newDisplayName.Trim();
        this.SetModified(dateTimeProvider.UtcNow);

        this.RaiseDomainEvent(new ServiceAccountDomainEvents.ServiceAccountUpdated(
            this.Id,
            dateTimeProvider.UtcNow));

        return Result.Success();
    }

    /// <summary>
    /// Updates the allowed scopes.
    /// </summary>
    public Result UpdateAllowedScopes(IReadOnlyList<string> newScopes, IDateTimeProvider dateTimeProvider)
    {
        if (newScopes.Count == 0)
        {
            return ServiceAccountErrors.ScopesRequired;
        }

        this.AllowedScopes = newScopes;
        this.SetModified(dateTimeProvider.UtcNow);

        this.RaiseDomainEvent(new ServiceAccountDomainEvents.ServiceAccountUpdated(
            this.Id,
            dateTimeProvider.UtcNow));

        return Result.Success();
    }

    /// <summary>
    /// Adds a new credential (client secret) to this service account.
    /// </summary>
    public Result<Guid> AddCredential(
        string secretHash,
        string? description,
        DateTimeOffset? expiresAt,
        IDateTimeProvider dateTimeProvider)
    {
        var credential = ClientCredential.Create(
            this.Id,
            secretHash,
            description,
            expiresAt,
            dateTimeProvider.UtcNow);

        this.credentials.Add(credential);
        this.SetModified(dateTimeProvider.UtcNow);

        this.RaiseDomainEvent(new ServiceAccountDomainEvents.CredentialAdded(
            this.Id,
            credential.Id,
            description,
            expiresAt,
            dateTimeProvider.UtcNow));

        return credential.Id;
    }

    /// <summary>
    /// Revokes a credential.
    /// </summary>
    public Result RevokeCredential(Guid credentialId, IDateTimeProvider dateTimeProvider)
    {
        ClientCredential? credential = this.credentials.FirstOrDefault(c => c.Id == credentialId);
        if (credential is null)
        {
            return ServiceAccountErrors.CredentialNotFound;
        }

        credential.MarkRevoked();
        this.SetModified(dateTimeProvider.UtcNow);

        this.RaiseDomainEvent(new ServiceAccountDomainEvents.CredentialRevoked(
            this.Id,
            credentialId,
            dateTimeProvider.UtcNow));

        return Result.Success();
    }

    /// <summary>
    /// Records that a credential was used.
    /// </summary>
    public void RecordCredentialUsage(Guid credentialId, IDateTimeProvider dateTimeProvider)
    {
        ClientCredential? credential = this.credentials.FirstOrDefault(c => c.Id == credentialId);
        credential?.UpdateLastUsed(dateTimeProvider.UtcNow);
    }

    /// <summary>
    /// Adds a new certificate to this service account.
    /// </summary>
    public Result<Guid> AddCertificate(
        string thumbprint,
        string subject,
        DateTimeOffset expiresAt,
        IDateTimeProvider dateTimeProvider)
    {
        // Check for duplicate thumbprint
        if (this.certificates.Any(c => c.Thumbprint.Equals(thumbprint, StringComparison.OrdinalIgnoreCase)))
        {
            return ServiceAccountErrors.CertificateThumbprintExists;
        }

        var certificate = ClientCertificate.Create(
            this.Id,
            thumbprint,
            subject,
            expiresAt,
            dateTimeProvider.UtcNow);

        this.certificates.Add(certificate);
        this.SetModified(dateTimeProvider.UtcNow);

        this.RaiseDomainEvent(new ServiceAccountDomainEvents.CertificateAdded(
            this.Id,
            certificate.Id,
            thumbprint,
            subject,
            expiresAt,
            dateTimeProvider.UtcNow));

        return certificate.Id;
    }

    /// <summary>
    /// Revokes a certificate.
    /// </summary>
    public Result RevokeCertificate(Guid certificateId, IDateTimeProvider dateTimeProvider)
    {
        ClientCertificate? certificate = this.certificates.FirstOrDefault(c => c.Id == certificateId);
        if (certificate is null)
        {
            return ServiceAccountErrors.CertificateNotFound;
        }

        certificate.MarkRevoked();
        this.SetModified(dateTimeProvider.UtcNow);

        this.RaiseDomainEvent(new ServiceAccountDomainEvents.CertificateRevoked(
            this.Id,
            certificateId,
            dateTimeProvider.UtcNow));

        return Result.Success();
    }

    /// <summary>
    /// Checks if this service account has at least one valid credential.
    /// </summary>
    public bool HasValidCredential(IDateTimeProvider dateTimeProvider)
    {
        return this.credentials.Any(c => c.IsValid(dateTimeProvider.UtcNow));
    }

    /// <summary>
    /// Checks if this service account has at least one valid certificate.
    /// </summary>
    public bool HasValidCertificate(IDateTimeProvider dateTimeProvider)
    {
        return this.certificates.Any(c => c.IsValid(dateTimeProvider.UtcNow));
    }

    /// <summary>
    /// Finds a valid credential by matching the secret hash.
    /// </summary>
    public ClientCredential? FindValidCredential(
        Func<string, bool> hashVerifier,
        IDateTimeProvider dateTimeProvider)
    {
        return this.credentials.FirstOrDefault(c => 
            c.IsValid(dateTimeProvider.UtcNow) && hashVerifier(c.SecretHash));
    }

    /// <summary>
    /// Finds a valid certificate by thumbprint.
    /// </summary>
    public ClientCertificate? FindValidCertificate(
        string thumbprint,
        IDateTimeProvider dateTimeProvider)
    {
        return this.certificates.FirstOrDefault(c => 
            c.IsValid(dateTimeProvider.UtcNow) && 
            c.Thumbprint.Equals(thumbprint, StringComparison.OrdinalIgnoreCase));
    }
}
