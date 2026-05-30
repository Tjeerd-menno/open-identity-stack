using SharedKernel;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications.Commands;

public sealed record DeleteApplicationCommand(DomainApplicationId ApplicationId);

public interface IDeleteApplicationUseCase
{
    Task<Result> ExecuteAsync(DeleteApplicationCommand command, CancellationToken cancellationToken = default);
}
