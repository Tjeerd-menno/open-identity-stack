using SharedKernel;

namespace OpenIdentityStack.Domain.Applications;

/// <summary>
/// Aggregate root for administrator-managed OAuth/OIDC software registrations.
/// </summary>
public sealed class Application : AggregateRoot<ApplicationId>
{
    private static readonly HashSet<string> supportedGrantTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization_code",
        "client_credentials",
        "refresh_token",
        "urn:ietf:params:oauth:grant-type:device_code"
    };

    private readonly List<string> allowedGrantTypes = [];
    private readonly List<string> allowedScopes = [];
    private readonly List<string> redirectUris = [];
    private readonly List<string> postLogoutRedirectUris = [];
    private readonly List<ApplicationCredential> credentials = [];

    public string ClientId { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public ApplicationType Type { get; private set; }

    public OAuthClientType ClientType { get; private set; }

    public ApplicationStatus Status { get; private set; }

    public IReadOnlyList<string> AllowedGrantTypes => this.allowedGrantTypes.AsReadOnly();

    public IReadOnlyList<string> AllowedScopes => this.allowedScopes.AsReadOnly();

    public IReadOnlyList<string> RedirectUris => this.redirectUris.AsReadOnly();

    public IReadOnlyList<string> PostLogoutRedirectUris => this.postLogoutRedirectUris.AsReadOnly();

    public bool RequirePkce { get; private set; }

    public bool RequireConsent { get; private set; }

    public IReadOnlyList<ApplicationCredential> Credentials => this.credentials.AsReadOnly();

    public bool RequiresMigrationReview { get; private set; }

    public string? MigrationSource { get; private set; }

    private Application()
    {
    }

    private Application(
        ApplicationId id,
        string clientId,
        string displayName,
        string? description,
        ApplicationType type,
        OAuthClientType clientType,
        IReadOnlyList<string> allowedGrantTypes,
        IReadOnlyList<string> allowedScopes,
        IReadOnlyList<string> redirectUris,
        IReadOnlyList<string> postLogoutRedirectUris,
        bool requirePkce,
        bool requireConsent,
        DateTimeOffset createdAt) : base(id)
    {
        this.ClientId = clientId;
        this.DisplayName = displayName;
        this.Description = description;
        this.Type = type;
        this.ClientType = clientType;
        this.Status = ApplicationStatus.Active;
        this.allowedGrantTypes.AddRange(allowedGrantTypes);
        this.allowedScopes.AddRange(allowedScopes);
        this.redirectUris.AddRange(redirectUris);
        this.postLogoutRedirectUris.AddRange(postLogoutRedirectUris);
        this.RequirePkce = requirePkce;
        this.RequireConsent = requireConsent;
        this.CreatedAt = createdAt;
        this.SetModified(createdAt);
    }

    public static Result<Application> Create(
        string clientId,
        string displayName,
        string? description,
        ApplicationType type,
        OAuthClientType clientType,
        IReadOnlyList<string> allowedGrantTypes,
        IReadOnlyList<string> allowedScopes,
        IReadOnlyList<string> redirectUris,
        IReadOnlyList<string> postLogoutRedirectUris,
        bool requirePkce,
        bool requireConsent,
        IDateTimeProvider dateTimeProvider)
    {
        Result<ValidatedApplicationConfiguration> validation = Validate(
            clientId,
            displayName,
            description,
            type,
            clientType,
            allowedGrantTypes,
            allowedScopes,
            redirectUris,
            postLogoutRedirectUris,
            requirePkce,
            requireConsent,
            originalType: null);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        ValidatedApplicationConfiguration values = validation.Value;
        var application = new Application(
            ApplicationId.NewId(),
            values.ClientId,
            values.DisplayName,
            values.Description,
            type,
            clientType,
            values.AllowedGrantTypes,
            values.AllowedScopes,
            values.RedirectUris,
            values.PostLogoutRedirectUris,
            requirePkce,
            requireConsent,
            dateTimeProvider.UtcNow);

        application.RaiseDomainEvent(new ApplicationDomainEvents.ApplicationCreated(
            application.Id,
            application.ClientId,
            application.DisplayName,
            dateTimeProvider.UtcNow));

        return application;
    }

    public static Result<Application> CreateMachineToMachine(
        string clientId,
        string displayName,
        string? description,
        IReadOnlyList<string> allowedScopes,
        IDateTimeProvider dateTimeProvider) =>
        Create(
            clientId,
            displayName,
            description,
            ApplicationType.MachineToMachine,
            OAuthClientType.Confidential,
            ["client_credentials"],
            allowedScopes,
            [],
            [],
            requirePkce: false,
            requireConsent: false,
            dateTimeProvider);

    public Result UpdateMetadata(string displayName, string? description, IDateTimeProvider dateTimeProvider)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return ApplicationErrors.DisplayNameRequired;
        }

        string trimmedDisplayName = displayName.Trim();
        if (trimmedDisplayName.Length > 255)
        {
            return ApplicationErrors.DisplayNameTooLong;
        }

        string? trimmedDescription = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();
        if (trimmedDescription?.Length > 1000)
        {
            return ApplicationErrors.DescriptionTooLong;
        }

        this.DisplayName = trimmedDisplayName;
        this.Description = trimmedDescription;
        this.SetModified(dateTimeProvider.UtcNow);
        this.RaiseDomainEvent(new ApplicationDomainEvents.ApplicationMetadataUpdated(this.Id, dateTimeProvider.UtcNow));

        return Result.Success();
    }

    public Result ConfigureOAuth(
        ApplicationType type,
        OAuthClientType clientType,
        IReadOnlyList<string> allowedGrantTypes,
        IReadOnlyList<string> allowedScopes,
        IReadOnlyList<string> redirectUris,
        IReadOnlyList<string> postLogoutRedirectUris,
        bool requirePkce,
        bool requireConsent,
        IDateTimeProvider dateTimeProvider)
    {
        Result<ValidatedApplicationConfiguration> validation = Validate(
            this.ClientId,
            this.DisplayName,
            this.Description,
            type,
            clientType,
            allowedGrantTypes,
            allowedScopes,
            redirectUris,
            postLogoutRedirectUris,
            requirePkce,
            requireConsent,
            originalType: this.Type);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        ValidatedApplicationConfiguration values = validation.Value;
        this.Type = values.Type;
        this.ClientType = clientType;
        this.allowedGrantTypes.Clear();
        this.allowedGrantTypes.AddRange(values.AllowedGrantTypes);
        this.allowedScopes.Clear();
        this.allowedScopes.AddRange(values.AllowedScopes);
        this.redirectUris.Clear();
        this.redirectUris.AddRange(values.RedirectUris);
        this.postLogoutRedirectUris.Clear();
        this.postLogoutRedirectUris.AddRange(values.PostLogoutRedirectUris);
        this.RequirePkce = values.RequirePkce;
        this.RequireConsent = values.RequireConsent;
        this.SetModified(dateTimeProvider.UtcNow);
        this.RaiseDomainEvent(new ApplicationDomainEvents.ApplicationOAuthConfigured(this.Id, dateTimeProvider.UtcNow));

        return Result.Success();
    }

    public Result Disable(IDateTimeProvider dateTimeProvider)
    {
        if (this.Status == ApplicationStatus.Disabled)
        {
            return ApplicationErrors.AlreadyDisabled;
        }

        this.Status = ApplicationStatus.Disabled;
        this.SetModified(dateTimeProvider.UtcNow);
        this.RaiseDomainEvent(new ApplicationDomainEvents.ApplicationDisabled(this.Id, dateTimeProvider.UtcNow));

        return Result.Success();
    }

    public Result Enable(IDateTimeProvider dateTimeProvider)
    {
        if (this.Status == ApplicationStatus.Active)
        {
            return ApplicationErrors.AlreadyActive;
        }

        this.Status = ApplicationStatus.Active;
        this.SetModified(dateTimeProvider.UtcNow);
        this.RaiseDomainEvent(new ApplicationDomainEvents.ApplicationEnabled(this.Id, dateTimeProvider.UtcNow));

        return Result.Success();
    }

    public Result Delete(IDateTimeProvider dateTimeProvider)
    {
        this.SetModified(dateTimeProvider.UtcNow);
        this.RaiseDomainEvent(new ApplicationDomainEvents.ApplicationDeleted(this.Id, dateTimeProvider.UtcNow));

        return Result.Success();
    }

    public Result<ApplicationCredential> AddSecret(
        string secretHash,
        string? description,
        DateTimeOffset? expiresAt,
        IDateTimeProvider dateTimeProvider)
    {
        if (this.ClientType == OAuthClientType.Public)
        {
            return ApplicationErrors.CredentialsNotAllowedForPublic;
        }

        Result<ApplicationCredential> result = ApplicationCredential.CreateClientSecret(
            this.Id,
            secretHash,
            description,
            expiresAt,
            dateTimeProvider);
        if (result.IsFailure)
        {
            return result.Error;
        }

        this.credentials.Add(result.Value);
        this.SetModified(dateTimeProvider.UtcNow);
        return result;
    }

    public Result<ApplicationCredential> AddCertificate(
        string thumbprint,
        string? subject,
        string? description,
        DateTimeOffset? expiresAt,
        IDateTimeProvider dateTimeProvider)
    {
        if (this.ClientType == OAuthClientType.Public)
        {
            return ApplicationErrors.CredentialsNotAllowedForPublic;
        }

        Result<ApplicationCredential> result = ApplicationCredential.CreateCertificate(
            this.Id,
            thumbprint,
            subject,
            description,
            expiresAt,
            dateTimeProvider);
        if (result.IsFailure)
        {
            return result.Error;
        }

        this.credentials.Add(result.Value);
        this.SetModified(dateTimeProvider.UtcNow);
        return result;
    }

    public Result RevokeCredential(Guid credentialId, IDateTimeProvider dateTimeProvider)
    {
        ApplicationCredential? credential = this.credentials.FirstOrDefault(item => item.Id == credentialId);
        if (credential is null)
        {
            return ApplicationErrors.CredentialNotFound;
        }

        Result result = credential.Revoke(dateTimeProvider);
        if (result.IsFailure)
        {
            return result;
        }

        this.SetModified(dateTimeProvider.UtcNow);
        return Result.Success();
    }

    private static Result<ValidatedApplicationConfiguration> Validate(
        string clientId,
        string displayName,
        string? description,
        ApplicationType type,
        OAuthClientType clientType,
        IReadOnlyList<string> allowedGrantTypes,
        IReadOnlyList<string> allowedScopes,
        IReadOnlyList<string> redirectUris,
        IReadOnlyList<string> postLogoutRedirectUris,
        bool requirePkce,
        bool requireConsent,
        ApplicationType? originalType)
    {
        if (originalType.HasValue && originalType.Value != type)
        {
            return ApplicationErrors.TypeChangeNotAllowed;
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            return ApplicationErrors.ClientIdRequired;
        }

        string trimmedClientId = clientId.Trim();
        if (trimmedClientId.Length > 255)
        {
            return ApplicationErrors.ClientIdTooLong;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return ApplicationErrors.DisplayNameRequired;
        }

        string trimmedDisplayName = displayName.Trim();
        if (trimmedDisplayName.Length > 255)
        {
            return ApplicationErrors.DisplayNameTooLong;
        }

        string? trimmedDescription = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();
        if (trimmedDescription?.Length > 1000)
        {
            return ApplicationErrors.DescriptionTooLong;
        }

        if (allowedGrantTypes.Count == 0)
        {
            return ApplicationErrors.GrantTypesRequired;
        }

        Result<IReadOnlyList<string>> grantTypesResult = NormalizeValues(allowedGrantTypes, ApplicationErrors.InvalidGrantType);
        if (grantTypesResult.IsFailure)
        {
            return grantTypesResult.Error;
        }

        foreach (string grantType in grantTypesResult.Value)
        {
            if (!supportedGrantTypes.Contains(grantType))
            {
                return ApplicationErrors.InvalidGrantType;
            }
        }

        Result<IReadOnlyList<string>> scopesResult = NormalizeValues(allowedScopes, ApplicationErrors.InvalidScope);
        if (scopesResult.IsFailure)
        {
            return scopesResult.Error;
        }

        Result<IReadOnlyList<string>> redirectUrisResult = NormalizeUris(redirectUris);
        if (redirectUrisResult.IsFailure)
        {
            return redirectUrisResult.Error;
        }

        Result<IReadOnlyList<string>> postLogoutUrisResult = NormalizeUris(postLogoutRedirectUris);
        if (postLogoutUrisResult.IsFailure)
        {
            return postLogoutUrisResult.Error;
        }

        ApplicationTypePolicy policy = ApplicationTypePolicyCatalog.GetPolicy(type);
        bool usesClientCredentials = grantTypesResult.Value.Contains("client_credentials", StringComparer.OrdinalIgnoreCase);
        bool usesAuthorizationCode = grantTypesResult.Value.Contains("authorization_code", StringComparer.OrdinalIgnoreCase);
        var requestedProfile = clientType.ToClientProfile();
        if (!policy.AllowedClientProfiles.Contains(requestedProfile))
        {
            return ApplicationErrors.InvalidClientType;
        }

        foreach (string grantType in grantTypesResult.Value)
        {
            if (!policy.AllowedGrantTypes.Contains(grantType))
            {
                return ApplicationErrors.InvalidGrantType;
            }
        }

        foreach (string defaultGrantType in policy.DefaultGrantTypes)
        {
            if (!grantTypesResult.Value.Contains(defaultGrantType, StringComparer.OrdinalIgnoreCase))
            {
                return ApplicationErrors.InvalidGrantType;
            }
        }

        if (usesClientCredentials && clientType != OAuthClientType.Confidential)
        {
            return ApplicationErrors.ConfidentialClientRequired;
        }

        if (policy.Options[ApplicationOptionKey.RedirectUris] == ApplicationOptionAvailability.Hidden &&
            redirectUrisResult.Value.Count > 0)
        {
            return ApplicationErrors.RedirectUrisNotAllowed;
        }

        if (policy.Options[ApplicationOptionKey.PostLogoutRedirectUris] == ApplicationOptionAvailability.Hidden &&
            postLogoutUrisResult.Value.Count > 0)
        {
            return ApplicationErrors.PostLogoutRedirectUrisNotAllowed;
        }

        if (policy.RequiresRedirectUris && redirectUrisResult.Value.Count == 0)
        {
            return ApplicationErrors.RedirectUriRequired;
        }

        if (usesAuthorizationCode && redirectUrisResult.Value.Count == 0)
        {
            return ApplicationErrors.RedirectUriRequired;
        }

        if (policy.Options[ApplicationOptionKey.Pkce] == ApplicationOptionAvailability.Hidden && requirePkce)
        {
            return ApplicationErrors.PkceNotAllowed;
        }

        if (policy.RequirePkce && !requirePkce)
        {
            return ApplicationErrors.PkceRequired;
        }

        if (usesAuthorizationCode && clientType == OAuthClientType.Public && !requirePkce)
        {
            return ApplicationErrors.PkceRequired;
        }

        if (policy.Options[ApplicationOptionKey.Consent] == ApplicationOptionAvailability.Hidden && requireConsent)
        {
            return ApplicationErrors.ConsentNotAllowed;
        }

        return new ValidatedApplicationConfiguration(
            trimmedClientId,
            trimmedDisplayName,
            trimmedDescription,
            type,
            grantTypesResult.Value,
            scopesResult.Value,
            redirectUrisResult.Value,
            postLogoutUrisResult.Value,
            requirePkce,
            requireConsent);
    }

    private static Result<IReadOnlyList<string>> NormalizeValues(IReadOnlyList<string> values, DomainError invalidError)
    {
        List<string> normalized = [];
        foreach (string? value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return invalidError;
            }

            normalized.Add(value.Trim());
        }

        return normalized;
    }

    private static Result<IReadOnlyList<string>> NormalizeUris(IReadOnlyList<string> uris)
    {
        List<string> normalized = [];
        foreach (string? uri in uris)
        {
            if (string.IsNullOrWhiteSpace(uri) || !Uri.TryCreate(uri.Trim(), UriKind.Absolute, out _))
            {
                return ApplicationErrors.InvalidRedirectUri;
            }

            normalized.Add(uri.Trim());
        }

        return normalized;
    }

    private sealed record ValidatedApplicationConfiguration(
        string ClientId,
        string DisplayName,
        string? Description,
        ApplicationType Type,
        IReadOnlyList<string> AllowedGrantTypes,
        IReadOnlyList<string> AllowedScopes,
        IReadOnlyList<string> RedirectUris,
        IReadOnlyList<string> PostLogoutRedirectUris,
        bool RequirePkce,
        bool RequireConsent);
}
