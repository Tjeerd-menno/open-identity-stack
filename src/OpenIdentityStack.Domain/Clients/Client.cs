using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Clients;

/// <summary>
/// Represents an OAuth2/OIDC client application that can authenticate users or access resources.
/// </summary>
public sealed class Client : AggregateRoot<ClientId>
{
    private readonly List<string> redirectUris = [];
    private readonly List<string> postLogoutRedirectUris = [];
    private readonly List<string> allowedScopes = [];
    private readonly List<string> allowedGrantTypes = [];

    /// <summary>
    /// Gets the OAuth2 client_id for this client application.
    /// </summary>
    public string ClientIdValue { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the display name for this client application.
    /// </summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the client type (confidential or public).
    /// </summary>
    public ClientType ClientType { get; private set; }

    /// <summary>
    /// Gets the optional description for this client application.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Gets the redirect URIs for this client (used in authorization code flow).
    /// </summary>
    public IReadOnlyList<string> RedirectUris => this.redirectUris.AsReadOnly();

    /// <summary>
    /// Gets the post-logout redirect URIs for this client.
    /// </summary>
    public IReadOnlyList<string> PostLogoutRedirectUris => this.postLogoutRedirectUris.AsReadOnly();

    /// <summary>
    /// Gets the allowed OAuth2 scopes for this client.
    /// </summary>
    public IReadOnlyList<string> AllowedScopes => this.allowedScopes.AsReadOnly();

    /// <summary>
    /// Gets the allowed OAuth2 grant types for this client.
    /// </summary>
    public IReadOnlyList<string> AllowedGrantTypes => this.allowedGrantTypes.AsReadOnly();

    /// <summary>
    /// Gets a value indicating whether PKCE is required for this client.
    /// </summary>
    public bool RequirePkce { get; private set; }

    /// <summary>
    /// Gets a value indicating whether consent is required for this client.
    /// </summary>
    public bool RequireConsent { get; private set; }

    // EF Core constructor
    private Client() : base() { }

    private Client(
        ClientId id,
        string clientIdValue,
        string displayName,
        ClientType clientType,
        string? description,
        IReadOnlyList<string> redirectUris,
        IReadOnlyList<string> postLogoutRedirectUris,
        IReadOnlyList<string> allowedScopes,
        IReadOnlyList<string> allowedGrantTypes,
        bool requirePkce,
        bool requireConsent,
        DateTimeOffset createdAt) : base(id)
    {
        this.ClientIdValue = clientIdValue;
        this.DisplayName = displayName;
        this.ClientType = clientType;
        this.Description = description;
        this.redirectUris.AddRange(redirectUris);
        this.postLogoutRedirectUris.AddRange(postLogoutRedirectUris);
        this.allowedScopes.AddRange(allowedScopes);
        this.allowedGrantTypes.AddRange(allowedGrantTypes);
        this.RequirePkce = requirePkce;
        this.RequireConsent = requireConsent;
        this.CreatedAt = createdAt;
        this.SetModified(createdAt);
    }

    /// <summary>
    /// Creates a new client application.
    /// </summary>
    public static Result<Client> Create(
        string clientIdValue,
        string displayName,
        ClientType clientType,
        string? description,
        IReadOnlyList<string> redirectUris,
        IReadOnlyList<string> postLogoutRedirectUris,
        IReadOnlyList<string> allowedScopes,
        IReadOnlyList<string> allowedGrantTypes,
        bool requirePkce,
        bool requireConsent,
        IDateTimeProvider dateTimeProvider)
    {
        // Validate client ID
        if (string.IsNullOrWhiteSpace(clientIdValue))
        {
            return ClientErrors.ClientIdRequired;
        }

        if (clientIdValue.Length > 255)
        {
            return ClientErrors.ClientIdTooLong;
        }

        // Validate display name
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return ClientErrors.DisplayNameRequired;
        }

        if (displayName.Length > 255)
        {
            return ClientErrors.DisplayNameTooLong;
        }

        // Validate redirect URIs (if any)
        foreach (string? uri in redirectUris)
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
            {
                return ClientErrors.InvalidRedirectUri;
            }
        }

        // Validate post-logout redirect URIs (if any)
        foreach (string? uri in postLogoutRedirectUris)
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
            {
                return ClientErrors.InvalidRedirectUri;
            }
        }

        // Validate scopes
        foreach (string? scope in allowedScopes)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                return ClientErrors.InvalidScope;
            }
        }

        // Validate grant types
        foreach (string? grantType in allowedGrantTypes)
        {
            if (string.IsNullOrWhiteSpace(grantType))
            {
                return ClientErrors.InvalidGrantType;
            }
        }

        // Validate redirect URIs are provided for authorization code flow
        if (allowedGrantTypes.Contains("authorization_code", StringComparer.OrdinalIgnoreCase) && redirectUris.Count == 0)
        {
            return ClientErrors.RedirectUriRequired;
        }

        var client = new Client(
            ClientId.NewId(),
            clientIdValue,
            displayName,
            clientType,
            description,
            redirectUris,
            postLogoutRedirectUris,
            allowedScopes,
            allowedGrantTypes,
            requirePkce,
            requireConsent,
            dateTimeProvider.UtcNow);

        return client;
    }

    /// <summary>
    /// Updates the client's basic information.
    /// </summary>
    public Result Update(
        string displayName,
        string? description,
        IDateTimeProvider dateTimeProvider)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return ClientErrors.DisplayNameRequired;
        }

        if (displayName.Length > 255)
        {
            return ClientErrors.DisplayNameTooLong;
        }

        this.DisplayName = displayName;
        this.Description = description;
        this.SetModified(dateTimeProvider.UtcNow);

        return Result.Success();
    }

    /// <summary>
    /// Updates the client's redirect URIs.
    /// </summary>
    public Result UpdateRedirectUris(
        IReadOnlyList<string> redirectUris,
        IReadOnlyList<string> postLogoutRedirectUris,
        IDateTimeProvider dateTimeProvider)
    {
        // Validate redirect URIs
        foreach (string? uri in redirectUris)
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
            {
                return ClientErrors.InvalidRedirectUri;
            }
        }

        // Validate post-logout redirect URIs
        foreach (string? uri in postLogoutRedirectUris)
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
            {
                return ClientErrors.InvalidRedirectUri;
            }
        }

        // Validate redirect URIs are provided for authorization code flow
        if (this.allowedGrantTypes.Contains("authorization_code", StringComparer.OrdinalIgnoreCase) && redirectUris.Count == 0)
        {
            return ClientErrors.RedirectUriRequired;
        }

        this.redirectUris.Clear();
        this.redirectUris.AddRange(redirectUris);

        this.postLogoutRedirectUris.Clear();
        this.postLogoutRedirectUris.AddRange(postLogoutRedirectUris);

        this.SetModified(dateTimeProvider.UtcNow);

        return Result.Success();
    }

    /// <summary>
    /// Updates the client's allowed scopes and grant types.
    /// </summary>
    public Result UpdatePermissions(
        IReadOnlyList<string> allowedScopes,
        IReadOnlyList<string> allowedGrantTypes,
        bool requirePkce,
        bool requireConsent,
        IDateTimeProvider dateTimeProvider)
    {
        // Validate scopes
        foreach (string? scope in allowedScopes)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                return ClientErrors.InvalidScope;
            }
        }

        // Validate grant types
        foreach (string? grantType in allowedGrantTypes)
        {
            if (string.IsNullOrWhiteSpace(grantType))
            {
                return ClientErrors.InvalidGrantType;
            }
        }

        // Validate redirect URIs are provided for authorization code flow
        if (allowedGrantTypes.Contains("authorization_code", StringComparer.OrdinalIgnoreCase) && this.redirectUris.Count == 0)
        {
            return ClientErrors.RedirectUriRequired;
        }

        this.allowedScopes.Clear();
        this.allowedScopes.AddRange(allowedScopes);

        this.allowedGrantTypes.Clear();
        this.allowedGrantTypes.AddRange(allowedGrantTypes);

        this.RequirePkce = requirePkce;
        this.RequireConsent = requireConsent;

        this.SetModified(dateTimeProvider.UtcNow);

        return Result.Success();
    }
}
