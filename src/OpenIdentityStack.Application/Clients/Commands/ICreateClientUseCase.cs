using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Application.Clients.Commands;

/// <summary>
/// Use case for creating a new OAuth2/OIDC client.
/// </summary>
public interface ICreateClientUseCase
{
    /// <summary>
    /// Executes the use case to create a new client.
    /// </summary>
    Task<Result<CreateClientResult>> ExecuteAsync(CreateClientCommand command, CancellationToken cancellationToken = default);
}
