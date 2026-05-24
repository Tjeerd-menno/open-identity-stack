using OpenIdentityStack.Domain.Applications;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications.Queries;

public sealed record ApplicationCredentialDetails(
    Guid Id,
    DomainApplicationId ApplicationId,
    ApplicationCredentialType Type,
    string? Thumbprint,
    string? Subject,
    string? Description,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt);
