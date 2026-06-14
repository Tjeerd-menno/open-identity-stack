using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications.Commands;

public sealed record UpdateApplicationMetadataCommand(
    DomainApplicationId ApplicationId,
    string DisplayName,
    string? Description);
