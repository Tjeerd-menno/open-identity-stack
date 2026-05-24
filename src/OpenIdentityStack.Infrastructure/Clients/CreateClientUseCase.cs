using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Clients;
using OpenIdentityStack.Application.Clients.Commands;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Domain.Common;
using OpenIddict.Abstractions;

using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;
using DomainClientType = OpenIdentityStack.Domain.Clients.ClientType;

namespace OpenIdentityStack.Infrastructure.Clients;

/// <summary>
/// Use case for creating a new OAuth2/OIDC client.
/// Manages both the domain Client entity and the OpenIddict application.
/// </summary>
public sealed class CreateClientUseCase : ICreateClientUseCase
{
    private readonly IClientRepository clientRepository;
    private readonly IOpenIddictApplicationManager applicationManager;
    private readonly IApplicationProtocolProjection projection;
    private readonly IDateTimeProvider dateTimeProvider;

    public CreateClientUseCase(
        IClientRepository clientRepository,
        IOpenIddictApplicationManager applicationManager,
        IApplicationProtocolProjection projection,
        IDateTimeProvider dateTimeProvider)
    {
        this.clientRepository = clientRepository;
        this.applicationManager = applicationManager;
        this.projection = projection;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<CreateClientResult>> ExecuteAsync(
        CreateClientCommand command,
        CancellationToken cancellationToken = default)
    {
        // Check if client ID already exists
        Client? existing = await this.clientRepository.GetByClientIdAsync(command.ClientId, cancellationToken);
        if (existing is not null)
        {
            return ClientErrors.ClientIdExists;
        }

        // Also check OpenIddict (in case of data inconsistency)
        object? existingApp = await this.applicationManager.FindByClientIdAsync(command.ClientId, cancellationToken);
        if (existingApp is not null)
        {
            return ClientErrors.ClientIdExists;
        }

        // Create the domain entity
        Result<Client> clientResult = Client.Create(
            command.ClientId,
            command.DisplayName,
            command.ClientType,
            command.Description,
            command.RedirectUris,
            command.PostLogoutRedirectUris,
            command.AllowedScopes,
            command.AllowedGrantTypes,
            command.RequirePkce,
            command.RequireConsent,
            this.dateTimeProvider);

        if (clientResult.IsFailure)
        {
            return clientResult.Error;
        }

        Client client = clientResult.Value;

        // Generate client secret for confidential clients
        string? clientSecret = null;
        if (command.ClientType == DomainClientType.Confidential)
        {
            clientSecret = GenerateSecureSecret();
        }

        Result<DomainApplication> applicationResult = DomainApplication.Create(
            command.ClientId,
            command.DisplayName,
            command.Description,
            InferApplicationType(command),
            command.ClientType == DomainClientType.Confidential
                ? OAuthClientType.Confidential
                : OAuthClientType.Public,
            command.AllowedGrantTypes,
            command.AllowedScopes,
            command.RedirectUris,
            command.PostLogoutRedirectUris,
            command.RequirePkce || command.ClientType == DomainClientType.Public,
            command.RequireConsent,
            this.dateTimeProvider);
        if (applicationResult.IsFailure)
        {
            return applicationResult.Error;
        }

        Result projectionResult = await this.projection.UpsertAsync(applicationResult.Value, clientSecret, cancellationToken);
        if (projectionResult.IsFailure)
        {
            return projectionResult.Error;
        }

        // Save the domain entity
        await this.clientRepository.AddAsync(client, cancellationToken);
        await this.clientRepository.SaveChangesAsync(cancellationToken);

        return new CreateClientResult(
            client.Id,
            client.ClientIdValue,
            client.DisplayName,
            clientSecret);
    }

    private static string GenerateSecureSecret()
    {
        const int secretLengthBytes = 32; // 256 bits
        // Generate a cryptographically secure random secret
        byte[] randomBytes = new byte[secretLengthBytes];
        System.Security.Cryptography.RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private static ApplicationType InferApplicationType(CreateClientCommand command)
    {
        if (command.AllowedGrantTypes.Count == 1 &&
            command.AllowedGrantTypes[0].Equals(OpenIddictConstants.GrantTypes.ClientCredentials, StringComparison.OrdinalIgnoreCase) &&
            command.ClientType == DomainClientType.Confidential &&
            command.RedirectUris.Count == 0 &&
            command.PostLogoutRedirectUris.Count == 0)
        {
            return ApplicationType.MachineToMachine;
        }

        if (command.AllowedGrantTypes.Any(grantType => grantType.Equals(OpenIddictConstants.GrantTypes.DeviceCode, StringComparison.OrdinalIgnoreCase)))
        {
            return ApplicationType.Device;
        }

        if (command.ClientType == DomainClientType.Public)
        {
            return ApplicationType.SinglePage;
        }

        return ApplicationType.Web;
    }
}
