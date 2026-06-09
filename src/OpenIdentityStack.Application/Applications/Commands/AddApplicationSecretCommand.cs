using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications.Commands;

public sealed record AddApplicationSecretCommand(
    DomainApplicationId ApplicationId,
    string? Description,
    DateTimeOffset? ExpiresAt,
    bool RevokeExisting);
