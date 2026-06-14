using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications.Commands;

public sealed record DeleteApplicationCommand(DomainApplicationId ApplicationId);
