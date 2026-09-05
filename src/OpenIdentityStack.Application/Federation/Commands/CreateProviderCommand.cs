
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;

using SharedKernel;
namespace OpenIdentityStack.Application.Federation.Commands;
/// <summary>
/// Command to create a new upstream provider.
/// </summary>
/// <param name="Name">The unique name/slug for the provider.</param>
/// <param name="DisplayName">The display name for UI purposes.</param>
/// <param name="Authority">The OIDC authority URL (issuer).</param>
/// <param name="ClientId">The OAuth2 client ID.</param>
/// <param name="ClientSecret">The OAuth2 client secret (optional).</param>
/// <param name="Scopes">The scopes to request from the provider.</param>
/// <param name="JitProvisioningEnabled">Whether JIT provisioning is enabled.</param>
public sealed record CreateProviderCommand(
    string Name,
    string DisplayName,
    string Authority,
    string ClientId,
    string? ClientSecret,
    IReadOnlyList<string>? Scopes,
    bool JitProvisioningEnabled = true);

/// <summary>
/// Result of creating an upstream provider.
/// </summary>
/// <param name="Id">The provider ID.</param>
/// <param name="Name">The unique name/slug.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Authority">The authority URL.</param>
/// <param name="ClientId">The client ID.</param>
/// <param name="Scopes">The scopes.</param>
/// <param name="JitProvisioningEnabled">Whether JIT provisioning is enabled.</param>
/// <param name="Status">The provider status.</param>
/// <param name="CreatedAt">When the provider was created.</param>
public sealed record CreateProviderResult(
    Guid Id,
    string Name,
    string DisplayName,
    string Authority,
    string ClientId,
    IReadOnlyList<string> Scopes,
    bool JitProvisioningEnabled,
    string Status,
    DateTimeOffset CreatedAt);

/// <summary>
/// Interface for the create provider use case.
/// </summary>
public interface ICreateProviderUseCase
{
    /// <summary>
    /// Executes the create provider command.
    /// </summary>
    /// <param name="command">The command containing provider details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the created provider details or an error.</returns>
    Task<Result<CreateProviderResult>> ExecuteAsync(
        CreateProviderCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case for creating a new upstream provider.
/// </summary>
public sealed class CreateProviderUseCase : ICreateProviderUseCase
{
    private readonly IUpstreamProviderRepository providerRepository;
    private readonly IEnvironmentProvider environmentProvider;
    private readonly ISecretProtector secretProtector;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateProviderUseCase"/> class.
    /// </summary>
    public CreateProviderUseCase(
        IUpstreamProviderRepository providerRepository,
        IEnvironmentProvider environmentProvider,
        ISecretProtector secretProtector)
    {
        this.providerRepository = providerRepository;
        this.environmentProvider = environmentProvider;
        this.secretProtector = secretProtector;
    }

    /// <inheritdoc />
    public async Task<Result<CreateProviderResult>> ExecuteAsync(
        CreateProviderCommand command,
        CancellationToken cancellationToken = default)
    {
        // Check for duplicate name
        UpstreamProvider? existingProvider = await this.providerRepository.GetByNameAsync(
            command.Name,
            cancellationToken);

        if (existingProvider is not null)
        {
            return DomainError.Conflict(
                "ProviderNameExists",
                $"A provider with name '{command.Name}' already exists.");
        }

        // Create the provider (allow HTTP only in development)
        Result<UpstreamProvider> createResult = UpstreamProvider.Create(
            command.Name,
            command.DisplayName,
            command.Authority,
            command.ClientId,
            allowInsecureHttp: this.environmentProvider.IsDevelopment);

        if (createResult.IsFailure)
        {
            return createResult.Error;
        }

        UpstreamProvider provider = createResult.Value;
        provider.SetJitProvisioningEnabled(command.JitProvisioningEnabled);

        // Set optional client secret
        if (!string.IsNullOrWhiteSpace(command.ClientSecret))
        {
            string protectedClientSecret;

            try
            {
                protectedClientSecret = this.secretProtector.Protect(command.ClientSecret);
            }
            catch (System.Exception)
            {
                return DomainError.Failure(
                    "ProviderClientSecretProtectionFailed",
                    "The provider client secret could not be protected.");
            }

            provider.SetClientSecret(protectedClientSecret);
        }

        // Store scopes if provider supports it (for now, scopes would be handled by the authentication handler)
        // The domain model might need extension for scopes storage

        await this.providerRepository.AddAsync(provider, cancellationToken);
        await this.providerRepository.SaveChangesAsync(cancellationToken);

        return new CreateProviderResult(
            provider.Id.Value,
            provider.Name,
            provider.DisplayName,
            provider.Authority,
            provider.ClientId,
            command.Scopes ?? [],
            provider.JitProvisioningEnabled,
            provider.Status.ToString(),
            provider.CreatedAt);
    }
}
