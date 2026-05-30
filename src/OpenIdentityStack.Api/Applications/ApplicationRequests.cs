using OpenIdentityStack.Domain.Applications;

namespace OpenIdentityStack.Api.Applications;

public sealed record CreateApplicationRequest(
    string ClientId,
    string DisplayName,
    string? Description,
    ApplicationProfile Profile,
    OAuthClientType ClientType,
    IReadOnlyList<string> AllowedGrantTypes,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    bool RequirePkce,
    bool RequireConsent,
    CreateInitialCredentialRequest? InitialCredential = null);

public sealed record CreateInitialCredentialRequest(
    string Type,
    string? Description,
    DateTimeOffset? ExpiresAt);

public sealed record UpdateApplicationMetadataRequest(
    string DisplayName,
    string? Description);

public sealed record ConfigureApplicationOAuthRequest(
    ApplicationProfile Profile,
    OAuthClientType ClientType,
    IReadOnlyList<string> AllowedGrantTypes,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    bool RequirePkce,
    bool RequireConsent);

public sealed record AddApplicationSecretRequest(
    string? Description,
    DateTimeOffset? ExpiresAt,
    bool RevokeExisting);

public sealed record AddApplicationCertificateRequest(
    string Thumbprint,
    string? Subject,
    string? Description,
    DateTimeOffset? ExpiresAt);

public sealed record ApplicationProfilePolicyResponse(
    ApplicationProfile ApplicationProfile,
    bool IsSelectable,
    string? UnavailabilityReason,
    ClientProfile DefaultClientProfile,
    IReadOnlyList<ClientProfile> AllowedClientProfiles,
    IReadOnlyList<string> AllowedGrantTypes,
    IReadOnlyList<string> DefaultGrantTypes,
    IReadOnlyDictionary<string, ApplicationOptionAvailability> Options,
    bool RequirePkce,
    bool DefaultRequirePkce,
    bool DefaultRequireConsent,
    bool RequiresRedirectUris);

public sealed record ApplicationCreatedResponse(
    Guid Id,
    string ClientId,
    string DisplayName,
    ApplicationProfile Profile,
    OAuthClientType ClientType,
    ApplicationStatus Status,
    string? InitialSecret,
    DateTimeOffset CreatedAt);

public sealed record ApplicationResponse(
    Guid Id,
    string ClientId,
    string DisplayName,
    string? Description,
    ApplicationProfile Profile,
    OAuthClientType ClientType,
    ApplicationStatus Status,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> AllowedGrantTypes,
    bool RequirePkce,
    bool RequireConsent,
    int CredentialCount,
    int CertificateCount,
    bool RequiresMigrationReview,
    string? MigrationSource,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

public sealed record ApplicationListItemResponse(
    Guid Id,
    string ClientId,
    string DisplayName,
    ApplicationProfile Profile,
    OAuthClientType ClientType,
    ApplicationStatus Status,
    IReadOnlyList<string> AllowedGrantTypes,
    int CredentialCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

public sealed record ListApplicationsResponse(
    IReadOnlyList<ApplicationListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);

public sealed record ApplicationCredentialResponse(
    Guid Id,
    Guid ApplicationId,
    ApplicationCredentialType Type,
    string? Thumbprint,
    string? Subject,
    string? Description,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt);

public sealed record AddApplicationSecretResponse(
    Guid CredentialId,
    string ClientSecret);

public sealed record AddApplicationCertificateResponse(Guid CredentialId);
