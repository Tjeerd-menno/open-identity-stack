using OpenIdentityStack.Application.Clients;
using OpenIdentityStack.Application.Clients.Commands;
using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Domain.Common;
using OpenIddict.Abstractions;

namespace OpenIdentityStack.Infrastructure.Clients;

/// <summary>
/// Use case for deleting a client.
/// Deletes both the domain Client entity and the OpenIddict application.
/// </summary>
public sealed class DeleteClientUseCase : IDeleteClientUseCase
{
    private readonly IClientRepository clientRepository;
    private readonly IOpenIddictApplicationManager applicationManager;

    public DeleteClientUseCase(
        IClientRepository clientRepository,
        IOpenIddictApplicationManager applicationManager)
    {
        this.clientRepository = clientRepository;
        this.applicationManager = applicationManager;
    }

    public async Task<Result> ExecuteAsync(
        DeleteClientCommand command,
        CancellationToken cancellationToken = default)
    {
        // Get the domain entity
        Client? client = await this.clientRepository.GetByIdAsync(command.ClientId, cancellationToken);
        if (client is null)
        {
            return ClientErrors.NotFound;
        }

        // Delete the OpenIddict application
        object? application = await this.applicationManager.FindByClientIdAsync(client.ClientIdValue, cancellationToken);
        if (application is not null)
        {
            await this.applicationManager.DeleteAsync(application, cancellationToken);
        }

        // Delete the domain entity
        await this.clientRepository.DeleteAsync(client, cancellationToken);
        await this.clientRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
