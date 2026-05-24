using OpenIdentityStack.Domain.Applications;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications.Queries;

public sealed record ApplicationDetails(
    DomainApplicationId Id,
    string ClientId,
    string DisplayName,
    string? Description,
    ApplicationType Type,
    OAuthClientType ClientType,
    ApplicationStatus Status,
    IReadOnlyList<string> AllowedGrantTypes,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    bool RequirePkce,
    bool RequireConsent,
    bool RequiresMigrationReview,
    string? MigrationSource,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);
