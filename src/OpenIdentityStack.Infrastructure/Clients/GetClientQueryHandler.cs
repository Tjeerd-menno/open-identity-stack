using OpenIdentityStack.Application.Clients;
using OpenIdentityStack.Application.Clients.Queries;
using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Clients;

/// <summary>
/// Query handler for getting a specific client.
/// </summary>
public sealed class GetClientQueryHandler : IGetClientQueryHandler
{
    private readonly IClientRepository clientRepository;

    public GetClientQueryHandler(IClientRepository clientRepository)
    {
        this.clientRepository = clientRepository;
    }

    public async Task<Result<ClientDetails>> HandleAsync(
        ClientId clientId,
        CancellationToken cancellationToken = default)
    {
        Client? client = await this.clientRepository.GetByIdAsync(clientId, cancellationToken);
        if (client is null)
        {
            return ClientErrors.NotFound;
        }

        var details = new ClientDetails(
            client.Id,
            client.ClientIdValue,
            client.DisplayName,
            client.ClientType,
            client.Description,
            client.RedirectUris,
            client.PostLogoutRedirectUris,
            client.AllowedScopes,
            client.AllowedGrantTypes,
            client.RequirePkce,
            client.RequireConsent,
            client.CreatedAt,
            client.ModifiedAt);

        return details;
    }
}
