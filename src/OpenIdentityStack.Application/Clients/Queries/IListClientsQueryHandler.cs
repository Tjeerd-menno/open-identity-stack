using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Application.Clients.Queries;

/// <summary>
/// Query handler for listing clients.
/// </summary>
public interface IListClientsQueryHandler
{
    /// <summary>
    /// Handles the query to list clients.
    /// </summary>
    Task<Result<ListClientsResult>> HandleAsync(ListClientsQuery query, CancellationToken cancellationToken = default);
}
