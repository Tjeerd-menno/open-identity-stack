using SharedKernel;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications.Commands;

public sealed record UpdateApplicationMetadataCommand(
    DomainApplicationId ApplicationId,
    string DisplayName,
    string? Description);

public interface IUpdateApplicationMetadataUseCase
{
    Task<Result<ApplicationCommandResult>> ExecuteAsync(
        UpdateApplicationMetadataCommand command,
        CancellationToken cancellationToken = default);
}
