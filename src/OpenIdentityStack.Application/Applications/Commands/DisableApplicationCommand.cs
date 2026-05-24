using SharedKernel;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications.Commands;

public sealed record DisableApplicationCommand(DomainApplicationId ApplicationId);

public interface IDisableApplicationUseCase
{
    Task<Result> ExecuteAsync(DisableApplicationCommand command, CancellationToken cancellationToken = default);
}
