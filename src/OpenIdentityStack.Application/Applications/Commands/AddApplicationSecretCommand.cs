using SharedKernel;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications.Commands;

public sealed record AddApplicationSecretCommand(
    DomainApplicationId ApplicationId,
    string? Description,
    DateTimeOffset? ExpiresAt,
    bool RevokeExisting);

public interface IAddApplicationSecretUseCase
{
    Task<Result<ApplicationCredentialCommandResult>> ExecuteAsync(
        AddApplicationSecretCommand command,
        CancellationToken cancellationToken = default);
}

