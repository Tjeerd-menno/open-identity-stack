using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Clients;
using OpenIdentityStack.Application.Clients.Commands;
using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Domain.Common;
using OpenIddict.Abstractions;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Clients;

/// <summary>
/// Use case for creating a new OAuth2/OIDC client.
/// Manages both the domain Client entity and the OpenIddict application.
/// </summary>
public sealed class CreateClientUseCase : ICreateClientUseCase
{
    private readonly IClientRepository clientRepository;
    private readonly IOpenIddictApplicationManager applicationManager;
    private readonly IDateTimeProvider dateTimeProvider;

    public CreateClientUseCase(
        IClientRepository clientRepository,
        IOpenIddictApplicationManager applicationManager,
        IDateTimeProvider dateTimeProvider)
    {
        this.clientRepository = clientRepository;
        this.applicationManager = applicationManager;
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
        if (command.ClientType == ClientType.Confidential)
        {
            clientSecret = GenerateSecureSecret();
        }

        // Create the OpenIddict application descriptor
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = command.ClientId,
            DisplayName = command.DisplayName,
            ClientType = command.ClientType == ClientType.Confidential
                ? OpenIddictConstants.ClientTypes.Confidential
                : OpenIddictConstants.ClientTypes.Public,
            ConsentType = command.RequireConsent
                ? OpenIddictConstants.ConsentTypes.Explicit
                : OpenIddictConstants.ConsentTypes.Implicit
        };

        // Set client secret for confidential clients
        if (clientSecret is not null)
        {
            descriptor.ClientSecret = clientSecret;
        }

        // Add redirect URIs
        foreach (string redirectUri in command.RedirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(redirectUri));
        }

        // Add post-logout redirect URIs
        foreach (string postLogoutUri in command.PostLogoutRedirectUris)
        {
            descriptor.PostLogoutRedirectUris.Add(new Uri(postLogoutUri));
        }

        // Add permissions based on grant types and scopes
        AddGrantTypePermissions(descriptor, command.AllowedGrantTypes);
        AddScopePermissions(descriptor, command.AllowedScopes);

        // Add PKCE requirement for public clients or if explicitly required
        if (command.RequirePkce || command.ClientType == ClientType.Public)
        {
            descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
        }

        // Create the OpenIddict application
        await this.applicationManager.CreateAsync(descriptor, cancellationToken);

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

    private static void AddGrantTypePermissions(
        OpenIddictApplicationDescriptor descriptor,
        IReadOnlyList<string> grantTypes)
    {
        foreach (string grantType in grantTypes)
        {
            switch (grantType.ToLowerInvariant())
            {
                case "client_credentials":
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
                    break;

                case "authorization_code":
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
                    break;

                case "refresh_token":
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
                    break;

                case "implicit":
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.Implicit);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.IdToken);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Token);
                    break;

                case "password":
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.Password);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
                    break;
            }
        }

        // Add common endpoints
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Introspection);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Revocation);

        // Add logout endpoint if authorization code flow is enabled
        if (grantTypes.Any(gt => gt.Equals("authorization_code", StringComparison.OrdinalIgnoreCase)))
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
        }
    }

    private static void AddScopePermissions(
        OpenIddictApplicationDescriptor descriptor,
        IReadOnlyList<string> scopes)
    {
        foreach (string scope in scopes)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
        }
    }
}
