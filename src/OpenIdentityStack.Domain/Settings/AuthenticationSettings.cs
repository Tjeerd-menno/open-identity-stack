using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;

using SharedKernel;
namespace OpenIdentityStack.Domain.Settings;

/// <summary>
/// Strongly-typed identifier for AuthenticationSettings.
/// </summary>
public readonly record struct AuthenticationSettingsId(Guid Value)
{
    /// <summary>
    /// Creates a new AuthenticationSettingsId.
    /// </summary>
    public static AuthenticationSettingsId Create() => new(Guid.NewGuid());

    /// <summary>
    /// Creates an AuthenticationSettingsId from an existing Guid.
    /// </summary>
    public static AuthenticationSettingsId From(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => this.Value.ToString();
}

/// <summary>
/// Represents the authentication configuration settings for the IAM system.
/// This is a singleton entity that stores the default authentication provider selection.
/// </summary>
public sealed class AuthenticationSettings
{
    /// <summary>
    /// The well-known identifier for the local authentication provider.
    /// </summary>
    public const string LocalProviderId = "local";

    private AuthenticationSettings(
        AuthenticationSettingsId id,
        string defaultProviderId,
        bool localFallbackEnabled,
        DateTimeOffset createdAt)
    {
        this.Id = id;
        this.DefaultProviderId = defaultProviderId;
        this.LocalFallbackEnabled = localFallbackEnabled;
        this.CreatedAt = createdAt;
        this.UpdatedAt = createdAt;
    }

    // EF Core constructor
    private AuthenticationSettings()
    {
        this.DefaultProviderId = LocalProviderId;
    }

    /// <summary>
    /// Gets the unique identifier.
    /// </summary>
    public AuthenticationSettingsId Id { get; private set; }

    /// <summary>
    /// Gets the ID of the default authentication provider.
    /// Use <see cref="LocalProviderId"/> for local accounts, or an UpstreamProviderId for external providers.
    /// </summary>
    public string DefaultProviderId { get; private set; } = LocalProviderId;

    /// <summary>
    /// Gets a value indicating whether local fallback is enabled when an external provider is default.
    /// When enabled, IAM admins can still use local credentials.
    /// </summary>
    public bool LocalFallbackEnabled { get; private set; } = true;

    /// <summary>
    /// Gets the timestamp when the settings were created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Gets the timestamp when the settings were last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Gets a value indicating whether local accounts is the default provider.
    /// </summary>
    public bool IsLocalDefault => this.DefaultProviderId == LocalProviderId;

    /// <summary>
    /// Creates a new AuthenticationSettings with default values.
    /// </summary>
    public static AuthenticationSettings CreateDefault(IDateTimeProvider dateTimeProvider)
    {
        return new AuthenticationSettings(
            AuthenticationSettingsId.Create(),
            LocalProviderId,
            localFallbackEnabled: true,
            dateTimeProvider.UtcNow);
    }

    /// <summary>
    /// Sets the default authentication provider to local accounts.
    /// </summary>
    public Result SetDefaultToLocal(IDateTimeProvider dateTimeProvider)
    {
        this.DefaultProviderId = LocalProviderId;
        this.UpdatedAt = dateTimeProvider.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Sets the default authentication provider to an external provider.
    /// </summary>
    public Result SetDefaultProvider(UpstreamProviderId providerId, IDateTimeProvider dateTimeProvider)
    {
        if (providerId.Value == Guid.Empty)
        {
            return AuthenticationSettingsErrors.InvalidProviderId;
        }

        this.DefaultProviderId = providerId.Value.ToString();
        this.UpdatedAt = dateTimeProvider.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Enables local fallback for IAM admins when an external provider is default.
    /// </summary>
    public Result EnableLocalFallback(IDateTimeProvider dateTimeProvider)
    {
        this.LocalFallbackEnabled = true;
        this.UpdatedAt = dateTimeProvider.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Disables local fallback when an external provider is default.
    /// </summary>
    public Result DisableLocalFallback(IDateTimeProvider dateTimeProvider)
    {
        this.LocalFallbackEnabled = false;
        this.UpdatedAt = dateTimeProvider.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Checks if a user can authenticate using local credentials.
    /// </summary>
    /// <param name="isIamAdmin">Whether the user is an IAM administrator.</param>
    /// <returns>True if local authentication is permitted.</returns>
    public bool CanAuthenticateLocally(bool isIamAdmin)
    {
        // If local is the default, anyone can authenticate locally
        if (this.IsLocalDefault)
        {
            return true;
        }

        // If external is default but local fallback is enabled, only admins can use local auth
        if (this.LocalFallbackEnabled && isIamAdmin)
        {
            return true;
        }

        return false;
    }
}

/// <summary>
/// Authentication settings related errors.
/// </summary>
public static class AuthenticationSettingsErrors
{
    public static readonly DomainError InvalidProviderId =
        DomainError.Validation("AuthenticationSettings.InvalidProviderId", "Provider ID is invalid.");

    public static readonly DomainError ProviderNotFound =
        DomainError.NotFound("AuthenticationSettings.ProviderNotFound", "The specified provider was not found.");

    public static readonly DomainError ProviderDisabled =
        DomainError.Validation("AuthenticationSettings.ProviderDisabled", "Cannot set a disabled provider as default.");

    public static readonly DomainError LocalAuthNotPermitted =
        DomainError.Forbidden("AuthenticationSettings.LocalAuthNotPermitted", "Authentication method not permitted.");
}
