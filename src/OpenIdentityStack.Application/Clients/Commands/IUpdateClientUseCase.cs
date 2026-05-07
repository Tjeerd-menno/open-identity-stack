using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Application.Clients.Commands;

/// <summary>
/// Use case for updating an existing client.
/// </summary>
public interface IUpdateClientUseCase
{
    /// <summary>
    /// Executes the use case to update a client.
    /// </summary>
    Task<Result<UpdateClientResult>> ExecuteAsync(UpdateClientCommand command, CancellationToken cancellationToken = default);
}
