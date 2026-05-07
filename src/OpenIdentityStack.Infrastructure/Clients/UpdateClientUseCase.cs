using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Clients;
using OpenIdentityStack.Application.Clients.Commands;
using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Domain.Common;
using OpenIddict.Abstractions;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Clients;

/// <summary>
/// Use case for updating an existing client.
/// Updates both the domain Client entity and the OpenIddict application.
/// </summary>
public sealed class UpdateClientUseCase : IUpdateClientUseCase
{
    private readonly IClientRepository clientRepository;
    private readonly IOpenIddictApplicationManager applicationManager;
    private readonly IDateTimeProvider dateTimeProvider;

    public UpdateClientUseCase(
        IClientRepository clientRepository,
        IOpenIddictApplicationManager applicationManager,
        IDateTimeProvider dateTimeProvider)
    {
        this.clientRepository = clientRepository;
        this.applicationManager = applicationManager;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<UpdateClientResult>> ExecuteAsync(
        UpdateClientCommand command,
        CancellationToken cancellationToken = default)
    {
        // Get the domain entity
        Client? client = await this.clientRepository.GetByIdAsync(command.ClientId, cancellationToken);
        if (client is null)
        {
            return ClientErrors.NotFound;
        }

        // Update the domain entity
        Result updateResult = client.Update(command.DisplayName, command.Description, this.dateTimeProvider);
        if (updateResult.IsFailure)
        {
            return updateResult.Error;
        }

        // Update the OpenIddict application
        object? application = await this.applicationManager.FindByClientIdAsync(client.ClientIdValue, cancellationToken);
        if (application is not null)
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await this.applicationManager.PopulateAsync(descriptor, application, cancellationToken);
            
            descriptor.DisplayName = command.DisplayName;
            
            await this.applicationManager.UpdateAsync(application, descriptor, cancellationToken);
        }

        // Save changes
        await this.clientRepository.SaveChangesAsync(cancellationToken);

        return new UpdateClientResult(client.Id, client.DisplayName, client.Description);
    }
}
