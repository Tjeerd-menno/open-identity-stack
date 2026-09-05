namespace OpenIdentityStack.Api.Federation;

/// <summary>
/// Request to create a new upstream provider.
/// </summary>
public sealed record CreateProviderRequest
{
    /// <summary>
    /// Gets or sets the unique name/slug for the provider.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the display name for UI purposes.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets or sets the OIDC authority URL (issuer).
    /// </summary>
    public required string Authority { get; init; }

    /// <summary>
    /// Gets or sets the OAuth2 client ID.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets or sets the OAuth2 client secret (optional).
    /// </summary>
    public string? ClientSecret { get; init; }

    /// <summary>
    /// Gets or sets the scopes to request from the provider.
    /// </summary>
    public IReadOnlyList<string>? Scopes { get; init; }

    /// <summary>
    /// Gets or sets whether JIT provisioning is enabled.
    /// </summary>
    public bool JitProvisioningEnabled { get; init; } = true;
}

/// <summary>
/// Request to update an existing upstream provider.
/// </summary>
public sealed record UpdateProviderRequest
{
    /// <summary>Authority replacement is unsupported; supplied values are rejected with migration guidance.</summary>
    public string? Authority { get; init; }
    /// <summary>
    /// Gets or sets the new display name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets or sets the new client ID.
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// Gets or sets the new client secret.
    /// </summary>
    public string? ClientSecret { get; init; }

    /// <summary>
    /// Gets or sets the new scopes.
    /// </summary>
    public IReadOnlyList<string>? Scopes { get; init; }

    /// <summary>
    /// Gets or sets whether JIT provisioning is enabled.
    /// </summary>
    public bool? JitProvisioningEnabled { get; init; }

    /// <summary>
    /// Gets or sets the new status ("Active" or "Disabled").
    /// </summary>
    public string? Status { get; init; }
}

/// <summary>
/// Response containing provider details.
/// </summary>
public sealed record ProviderResponse
{
    /// <summary>
    /// Gets or sets the provider ID.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets or sets the unique name/slug.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the authority URL.
    /// </summary>
    public string Authority { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the client ID.
    /// </summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the scopes.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];

    /// <summary>
    /// Gets or sets whether JIT provisioning is enabled.
    /// </summary>
    public bool JitProvisioningEnabled { get; init; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets when the provider was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }
}
