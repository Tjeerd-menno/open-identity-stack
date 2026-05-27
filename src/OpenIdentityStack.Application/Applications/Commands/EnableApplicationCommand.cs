using SharedKernel;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications.Commands;

public sealed record EnableApplicationCommand(DomainApplicationId ApplicationId);

public interface IEnableApplicationUseCase
{
    Task<Result> ExecuteAsync(EnableApplicationCommand command, CancellationToken cancellationToken = default);
}
