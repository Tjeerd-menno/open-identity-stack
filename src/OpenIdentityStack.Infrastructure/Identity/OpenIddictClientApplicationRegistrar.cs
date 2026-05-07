using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.ServiceAccounts;
using OpenIddict.Abstractions;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>
/// Registers service accounts as confidential OpenIddict clients.
/// </summary>
public sealed class OpenIddictClientApplicationRegistrar : IClientApplicationRegistrar
{
    private readonly IOpenIddictApplicationManager applicationManager;
    private readonly IOpenIddictScopeManager scopeManager;
    private readonly ILogger<OpenIddictClientApplicationRegistrar> logger;

    public OpenIddictClientApplicationRegistrar(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        ILogger<OpenIddictClientApplicationRegistrar> logger)
    {
        this.applicationManager = applicationManager;
        this.scopeManager = scopeManager;
        this.logger = logger;
    }

    public async Task<Result> RegisterServiceAccountAsync(
        string clientId,
        string displayName,
        string clientSecret,
        IReadOnlyList<string> allowedScopes,
        IReadOnlyList<string> allowedGrantTypes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            object? existing = await this.applicationManager.FindByClientIdAsync(clientId, cancellationToken);
            if (existing is not null)
            {
                return ServiceAccountErrors.ClientIdAlreadyExists(clientId);
            }

            await this.EnsureScopesAsync(allowedScopes, cancellationToken);

            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
                DisplayName = displayName,
            };

            AddGrantTypePermissions(descriptor, allowedGrantTypes);
            foreach (string scope in allowedScopes.Distinct(StringComparer.Ordinal))
            {
                descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
            }

            await this.applicationManager.CreateAsync(descriptor, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.RegistrationFailed(this.logger, ex, clientId);
            return ServiceAccountErrors.ClientRegistrationFailed;
        }
    }

    private async Task EnsureScopesAsync(
        IReadOnlyList<string> allowedScopes,
        CancellationToken cancellationToken)
    {
        foreach (string scope in allowedScopes.Distinct(StringComparer.Ordinal))
        {
            if (await this.scopeManager.FindByNameAsync(scope, cancellationToken) is not null)
            {
                continue;
            }

            await this.scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = scope,
                DisplayName = $"{scope} Scope"
            }, cancellationToken);
        }
    }

    private static void AddGrantTypePermissions(
        OpenIddictApplicationDescriptor descriptor,
        IReadOnlyList<string> grantTypes)
    {
        foreach (string grantType in grantTypes)
        {
            switch (grantType)
            {
                case OpenIddictConstants.GrantTypes.ClientCredentials:
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Service accounts only support the '{OpenIddictConstants.GrantTypes.ClientCredentials}' grant type.");
            }
        }

        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Introspection);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Revocation);
    }
}

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to register OAuth client for service account '{ClientId}'.")]
    internal static partial void RegistrationFailed(ILogger logger, Exception ex, string clientId);
}
