namespace OpenIdentityStack.Api.Settings;

/// <summary>
/// Request to set the default authentication provider.
/// </summary>
public sealed record SetDefaultProviderRequest
{
    /// <summary>
    /// The provider ID. Use "local" for local accounts, or a GUID for external providers.
    /// </summary>
    public required string ProviderId { get; init; }
}

/// <summary>
/// Request to set local fallback configuration.
/// </summary>
public sealed record SetLocalFallbackRequest
{
    /// <summary>
    /// Whether local fallback should be enabled.
    /// </summary>
    public required bool Enabled { get; init; }
}

/// <summary>
/// Response with authentication settings.
/// </summary>
public sealed record AuthenticationSettingsResponse
{
    /// <summary>
    /// The ID of the default provider.
    /// </summary>
    public required string DefaultProviderId { get; init; }

    /// <summary>
    /// Whether local accounts is the default provider.
    /// </summary>
    public required bool IsLocalDefault { get; init; }

    /// <summary>
    /// Whether local fallback is enabled for IAM admins when external provider is default.
    /// </summary>
    public required bool LocalFallbackEnabled { get; init; }

    /// <summary>
    /// When the settings were last updated.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Response with authentication provider information.
/// </summary>
public sealed record AuthenticationProviderResponse
{
    /// <summary>
    /// The provider ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The provider name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The display name for UI.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// The provider type (local or external).
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Whether the provider is active.
    /// </summary>
    public required bool IsActive { get; init; }
}
