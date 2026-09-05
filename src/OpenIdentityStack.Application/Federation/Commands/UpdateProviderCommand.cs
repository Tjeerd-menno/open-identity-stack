
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;

using SharedKernel;
namespace OpenIdentityStack.Application.Federation.Commands;
/// <summary>
/// Command to update an existing upstream provider.
/// </summary>
/// <param name="ProviderId">The provider ID to update.</param>
/// <param name="DisplayName">The new display name (optional).</param>
/// <param name="ClientId">The new client ID (optional).</param>
/// <param name="ClientSecret">The new client secret (optional).</param>
/// <param name="Scopes">The new scopes (optional).</param>
/// <param name="JitProvisioningEnabled">Whether JIT provisioning is enabled (optional).</param>
/// <param name="Status">The new status: "Active" or "Disabled" (optional).</param>
public sealed record UpdateProviderCommand(
    Guid ProviderId,
    string? DisplayName = null,
    string? ClientId = null,
    string? ClientSecret = null,
    IReadOnlyList<string>? Scopes = null,
    bool? JitProvisioningEnabled = null,
    string? Status = null);

/// <summary>
/// Result of updating an upstream provider.
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
public sealed record UpdateProviderResult(
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
/// Interface for the update provider use case.
/// </summary>
public interface IUpdateProviderUseCase
{
    /// <summary>
    /// Executes the update provider command.
    /// </summary>
    /// <param name="command">The command containing update details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the updated provider details or an error.</returns>
    Task<Result<UpdateProviderResult>> ExecuteAsync(
        UpdateProviderCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case for updating an existing upstream provider.
/// </summary>
public sealed class UpdateProviderUseCase : IUpdateProviderUseCase
{
    private readonly IUpstreamProviderRepository providerRepository;
    private readonly ISecretProtector secretProtector;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateProviderUseCase"/> class.
    /// </summary>
    public UpdateProviderUseCase(
        IUpstreamProviderRepository providerRepository,
        ISecretProtector? secretProtector = null)
    {
        this.providerRepository = providerRepository;
        this.secretProtector = secretProtector ?? new NoopSecretProtector();
    }

    /// <inheritdoc />
    public async Task<Result<UpdateProviderResult>> ExecuteAsync(
        UpdateProviderCommand command,
        CancellationToken cancellationToken = default)
    {
        var providerId = UpstreamProviderId.From(command.ProviderId);
        UpstreamProvider? provider = await this.providerRepository.GetByIdAsync(providerId, cancellationToken);

        if (provider is null)
        {
            return DomainError.NotFound(
                "ProviderNotFound",
                $"Provider with ID '{command.ProviderId}' was not found.");
        }

        // Update display name
        if (!string.IsNullOrWhiteSpace(command.DisplayName))
        {
            Result updateResult = provider.UpdateDisplayName(command.DisplayName);
            if (updateResult.IsFailure)
            {
                return updateResult.Error;
            }
        }

        // Update client ID
        if (!string.IsNullOrWhiteSpace(command.ClientId))
        {
            Result updateResult = provider.UpdateClientId(command.ClientId);
            if (updateResult.IsFailure)
            {
                return updateResult.Error;
            }
        }

        // Update client secret
        if (command.ClientSecret is not null)
        {
            if (string.IsNullOrWhiteSpace(command.ClientSecret))
            {
                provider.SetClientSecret(null);
            }
            else
            {
                try
                {
                    provider.SetClientSecret(this.secretProtector.Protect(command.ClientSecret));
                }
                catch (Exception)
                {
                    return DomainError.Failure(
                        "Federation.ProviderSecretProtectionFailed",
                        "Failed to protect the provider client secret.");
                }
            }
        }

        // Update status
        if (!string.IsNullOrWhiteSpace(command.Status))
        {
            if (command.Status.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
            {
                Result disableResult = provider.Disable();
                if (disableResult.IsFailure)
                {
                    return disableResult.Error;
                }
            }
            else if (command.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                Result enableResult = provider.Enable();
                if (enableResult.IsFailure)
                {
                    return enableResult.Error;
                }
            }
        }

        if (command.JitProvisioningEnabled is { } jitProvisioningEnabled)
        {
            provider.SetJitProvisioningEnabled(jitProvisioningEnabled);
        }

        await this.providerRepository.SaveChangesAsync(cancellationToken);

        return new UpdateProviderResult(
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

    private sealed class NoopSecretProtector : ISecretProtector
    {
        public string Protect(string secret) => secret;

        public string? Unprotect(string? protectedSecret) => protectedSecret;
    }
}
