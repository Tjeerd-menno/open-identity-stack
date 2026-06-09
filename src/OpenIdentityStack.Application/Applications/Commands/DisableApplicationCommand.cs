using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications.Commands;

public sealed record DisableApplicationCommand(DomainApplicationId ApplicationId);
