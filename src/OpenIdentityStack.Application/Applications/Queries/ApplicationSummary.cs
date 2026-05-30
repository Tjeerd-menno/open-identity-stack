using OpenIdentityStack.Domain.Applications;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications.Queries;

public sealed record ApplicationSummary(
    DomainApplicationId Id,
    string ClientId,
    string DisplayName,
    ApplicationProfile Profile,
    OAuthClientType ClientType,
    ApplicationStatus Status,
    bool RequiresMigrationReview,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);
