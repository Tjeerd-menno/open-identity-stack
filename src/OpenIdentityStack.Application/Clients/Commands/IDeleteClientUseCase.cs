using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Application.Clients.Commands;

/// <summary>
/// Use case for deleting a client.
/// </summary>
public interface IDeleteClientUseCase
{
    /// <summary>
    /// Executes the use case to delete a client.
    /// </summary>
    Task<Result> ExecuteAsync(DeleteClientCommand command, CancellationToken cancellationToken = default);
}
