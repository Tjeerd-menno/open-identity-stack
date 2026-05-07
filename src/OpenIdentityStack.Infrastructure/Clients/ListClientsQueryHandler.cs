using OpenIdentityStack.Application.Clients;
using OpenIdentityStack.Application.Clients.Queries;
using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Clients;

/// <summary>
/// Query handler for listing clients.
/// </summary>
public sealed class ListClientsQueryHandler : IListClientsQueryHandler
{
    private readonly IClientRepository clientRepository;

    public ListClientsQueryHandler(IClientRepository clientRepository)
    {
        this.clientRepository = clientRepository;
    }

    public async Task<Result<ListClientsResult>> HandleAsync(
        ListClientsQuery query,
        CancellationToken cancellationToken = default)
    {
        (IReadOnlyList<Client> items, int totalCount) = await this.clientRepository.ListAsync(
            query.Page,
            query.PageSize,
            cancellationToken);

        var listItems = items.Select(c => new ClientListItem(
            c.Id,
            c.ClientIdValue,
            c.DisplayName,
            c.ClientType,
            c.AllowedGrantTypes,
            c.CreatedAt)).ToList();

        return new ListClientsResult(listItems, totalCount, query.Page, query.PageSize);
    }
}
