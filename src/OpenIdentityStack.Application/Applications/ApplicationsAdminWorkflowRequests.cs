using OpenIdentityStack.Application.Applications.Queries;
using OpenIdentityStack.Domain.Applications;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications;

public sealed record ListApplicationsAdminWorkflowRequest(
    int Page = 1,
    int PageSize = 20,
    ApplicationProfile? Profile = null,
    ApplicationStatus? Status = null,
    OAuthClientType? ClientType = null,
    string? SearchTerm = null);

public sealed record GetApplicationAdminWorkflowRequest(DomainApplicationId ApplicationId);

public sealed record CreateApplicationAdminWorkflowRequest(
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
    CreateInitialCredentialAdminWorkflowRequest? InitialCredential = null);

public sealed record CreateInitialCredentialAdminWorkflowRequest(
    string? Type,
    string? Description,
    DateTimeOffset? ExpiresAt);

public sealed record ApplicationCreateOperationResult(
    ApplicationDetails Details,
    string? InitialSecret);

public sealed record UpdateApplicationMetadataAdminWorkflowRequest(
    DomainApplicationId ApplicationId,
    string DisplayName,
    string? Description);

public sealed record ConfigureApplicationOAuthAdminWorkflowRequest(
    DomainApplicationId ApplicationId,
    ApplicationProfile Profile,
    OAuthClientType ClientType,
    IReadOnlyList<string> AllowedGrantTypes,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    bool RequirePkce,
    bool RequireConsent);

public sealed record EnableApplicationAdminWorkflowRequest(DomainApplicationId ApplicationId);

public sealed record DisableApplicationAdminWorkflowRequest(DomainApplicationId ApplicationId);

public sealed record DeleteApplicationAdminWorkflowRequest(DomainApplicationId ApplicationId);

public sealed record ListApplicationCredentialsAdminWorkflowRequest(DomainApplicationId ApplicationId);

public sealed record AddApplicationSecretAdminWorkflowRequest(
    DomainApplicationId ApplicationId,
    string? Description,
    DateTimeOffset? ExpiresAt,
    bool RevokeExisting);

public sealed record ApplicationSecretOperationResult(
    Guid CredentialId,
    string OneTimeSecret);

public sealed record AddApplicationCertificateAdminWorkflowRequest(
    DomainApplicationId ApplicationId,
    string Thumbprint,
    string? Subject,
    string? Description,
    DateTimeOffset? ExpiresAt);

public sealed record RevokeApplicationCredentialAdminWorkflowRequest(
    DomainApplicationId ApplicationId,
    Guid CredentialId);

public sealed record ListApplicationProfilePoliciesAdminWorkflowRequest;
