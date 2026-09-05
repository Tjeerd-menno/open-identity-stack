using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Federation;
/// <summary>
/// Strongly-typed identifier for UpstreamProvider.
/// </summary>
public readonly record struct UpstreamProviderId(Guid Value)
{
    /// <summary>
    /// Creates a new UpstreamProviderId.
    /// </summary>
    public static UpstreamProviderId Create() => new(Guid.NewGuid());

    /// <summary>
    /// Creates an UpstreamProviderId from an existing Guid.
    /// </summary>
    public static UpstreamProviderId From(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => this.Value.ToString();
}

/// <summary>
/// Represents an upstream identity provider (external OIDC/OAuth2 provider).
/// </summary>
public sealed class UpstreamProvider
{
    private readonly List<ClaimMapping> claimMappings = [];

    private UpstreamProvider(
        UpstreamProviderId id,
        string name,
        string displayName,
        string authority,
        string clientId)
    {
        this.Id = id;
        this.Name = name;
        this.DisplayName = displayName;
        this.Authority = authority;
        this.ClientId = clientId;
        this.Status = ProviderStatus.Active;
        this.CreatedAt = DateTimeOffset.UtcNow;
        this.UpdatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core constructor
    private UpstreamProvider()
    {
        this.Name = string.Empty;
        this.DisplayName = string.Empty;
        this.Authority = string.Empty;
        this.ClientId = string.Empty;
    }

    /// <summary>
    /// Gets the unique identifier.
    /// </summary>
    public UpstreamProviderId Id { get; private set; }

    /// <summary>
    /// Gets the unique name/identifier for the provider (e.g., "azure-ad", "google").
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the display name for UI purposes.
    /// </summary>
    public string DisplayName { get; private set; }

    /// <summary>
    /// Gets the OIDC authority URL (issuer).
    /// </summary>
    public string Authority { get; private set; }

    /// <summary>
    /// Gets the OAuth2 client ID.
    /// </summary>
    public string ClientId { get; private set; }

    /// <summary>
    /// Gets the OAuth2 client secret (optional for public clients).
    /// </summary>
    public string? ClientSecret { get; private set; }

    /// <summary>
    /// Gets the provider status.
    /// </summary>
    public ProviderStatus Status { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the provider is active.
    /// </summary>
    public bool IsActive => this.Status == ProviderStatus.Active;

    /// <summary>
    /// Gets whether an authenticated upstream identity may provision a new local account.
    /// </summary>
    public bool JitProvisioningEnabled { get; private set; } = true;

    /// <summary>
    /// Changes the provisioning policy without affecting existing linked accounts.
    /// </summary>
    public void SetJitProvisioningEnabled(bool enabled)
    {
        this.JitProvisioningEnabled = enabled;
        this.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the claim mappings for this provider.
    /// </summary>
    public IReadOnlyList<ClaimMapping> ClaimMappings => this.claimMappings.AsReadOnly();

    /// <summary>
    /// Gets the timestamp when the provider was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Gets the timestamp when the provider was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Creates a new upstream provider.
    /// </summary>
    /// <param name="name">The unique name/identifier for the provider.</param>
    /// <param name="displayName">The display name for UI purposes.</param>
    /// <param name="authority">The OIDC authority URL (issuer).</param>
    /// <param name="clientId">The OAuth2 client ID.</param>
    /// <param name="allowInsecureHttp">Whether to allow HTTP (should only be true in development).</param>
    public static Result<UpstreamProvider> Create(
        string name,
        string displayName,
        string authority,
        string clientId,
        bool allowInsecureHttp = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return UpstreamProviderErrors.NameRequired;
        }

        if (string.IsNullOrWhiteSpace(authority))
        {
            return UpstreamProviderErrors.AuthorityRequired;
        }

        if (!Uri.TryCreate(authority, UriKind.Absolute, out Uri? authorityUri))
        {
            return UpstreamProviderErrors.AuthorityInvalidUrl;
        }

        // Enforce HTTPS unless explicitly allowed for development
        if (authorityUri.Scheme == "http" && !allowInsecureHttp)
        {
            return UpstreamProviderErrors.HttpNotAllowed;
        }

        if (authorityUri.Scheme != "https" && authorityUri.Scheme != "http")
        {
            return UpstreamProviderErrors.AuthorityInvalidUrl;
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            return UpstreamProviderErrors.ClientIdRequired;
        }

        return new UpstreamProvider(
            UpstreamProviderId.Create(),
            name.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim(),
            authority.TrimEnd('/'),
            clientId.Trim());
    }

    /// <summary>
    /// Sets the client secret.
    /// </summary>
    public Result SetClientSecret(string? secret)
    {
        this.ClientSecret = string.IsNullOrWhiteSpace(secret) ? null : secret;
        this.UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Disables the provider.
    /// </summary>
    public Result Disable()
    {
        if (this.Status == ProviderStatus.Disabled)
        {
            return UpstreamProviderErrors.AlreadyDisabled;
        }

        this.Status = ProviderStatus.Disabled;
        this.UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Enables the provider.
    /// </summary>
    public Result Enable()
    {
        if (this.Status == ProviderStatus.Active)
        {
            return UpstreamProviderErrors.AlreadyActive;
        }

        this.Status = ProviderStatus.Active;
        this.UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Adds a claim mapping.
    /// </summary>
    public Result AddClaimMapping(ClaimMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        if (this.claimMappings.Any(m => m.SourceClaim == mapping.SourceClaim))
        {
            return UpstreamProviderErrors.DuplicateClaimMapping;
        }

        this.claimMappings.Add(mapping);
        this.UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Removes a claim mapping by source claim name.
    /// </summary>
    public Result RemoveClaimMapping(string sourceClaim)
    {
        ClaimMapping? mapping = this.claimMappings.FirstOrDefault(m => m.SourceClaim == sourceClaim);
        if (mapping is null)
        {
            return UpstreamProviderErrors.ClaimMappingNotFound;
        }

        this.claimMappings.Remove(mapping);
        this.UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Updates the display name.
    /// </summary>
    public Result UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return UpstreamProviderErrors.DisplayNameRequired;
        }

        this.DisplayName = displayName.Trim();
        this.UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Updates the authority URL.
    /// </summary>
    /// <param name="authority">The new authority URL.</param>
    /// <param name="allowInsecureHttp">Whether to allow HTTP (should only be true in development).</param>
    public Result UpdateAuthority(string authority, bool allowInsecureHttp = false)
    {
        if (string.IsNullOrWhiteSpace(authority))
        {
            return UpstreamProviderErrors.AuthorityRequired;
        }

        if (!Uri.TryCreate(authority, UriKind.Absolute, out Uri? authorityUri))
        {
            return UpstreamProviderErrors.AuthorityInvalidUrl;
        }

        // Enforce HTTPS unless explicitly allowed for development
        if (authorityUri.Scheme == "http" && !allowInsecureHttp)
        {
            return UpstreamProviderErrors.HttpNotAllowed;
        }

        if (authorityUri.Scheme != "https" && authorityUri.Scheme != "http")
        {
            return UpstreamProviderErrors.AuthorityInvalidUrl;
        }

        this.Authority = authority.TrimEnd('/');
        this.UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Updates the client ID.
    /// </summary>
    public Result UpdateClientId(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return UpstreamProviderErrors.ClientIdRequired;
        }

        this.ClientId = clientId.Trim();
        this.UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }
}

/// <summary>
/// Upstream provider related errors.
/// </summary>
public static class UpstreamProviderErrors
{
    public static readonly DomainError NameRequired =
        DomainError.Validation("UpstreamProvider.NameRequired", "Provider name is required.");

    public static readonly DomainError DisplayNameRequired =
        DomainError.Validation("UpstreamProvider.DisplayNameRequired", "Display name is required.");

    public static readonly DomainError AuthorityRequired =
        DomainError.Validation("UpstreamProvider.AuthorityRequired", "Authority URL is required.");

    public static readonly DomainError AuthorityInvalidUrl =
        DomainError.Validation("UpstreamProvider.AuthorityInvalidUrl", "Authority must be a valid HTTP/HTTPS URL.");

    public static readonly DomainError HttpNotAllowed =
        DomainError.Validation("UpstreamProvider.HttpNotAllowed", "HTTP is not allowed for authority URLs. Use HTTPS for secure communication.");

    public static readonly DomainError ClientIdRequired =
        DomainError.Validation("UpstreamProvider.ClientIdRequired", "Client ID is required.");

    public static readonly DomainError AlreadyDisabled =
        DomainError.Conflict("UpstreamProvider.AlreadyDisabled", "Provider is already disabled.");

    public static readonly DomainError AlreadyActive =
        DomainError.Conflict("UpstreamProvider.AlreadyActive", "Provider is already active.");

    public static readonly DomainError DuplicateClaimMapping =
        DomainError.Conflict("UpstreamProvider.DuplicateClaimMapping", "A mapping for this source claim already exists.");

    public static readonly DomainError ClaimMappingNotFound =
        DomainError.NotFound("UpstreamProvider.ClaimMappingNotFound", "Claim mapping not found.");

    public static readonly DomainError NotFound =
        DomainError.NotFound("UpstreamProvider.NotFound", "Upstream provider not found.");

    public static readonly DomainError ProviderDisabled =
        DomainError.Forbidden("UpstreamProvider.Disabled", "This identity provider is currently disabled.");
}
