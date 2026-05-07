using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Application.Clients.Queries;

/// <summary>
/// Query handler for getting a specific client.
/// </summary>
public interface IGetClientQueryHandler
{
    /// <summary>
    /// Handles the query to get a client by ID.
    /// </summary>
    Task<Result<ClientDetails>> HandleAsync(ClientId clientId, CancellationToken cancellationToken = default);
}
