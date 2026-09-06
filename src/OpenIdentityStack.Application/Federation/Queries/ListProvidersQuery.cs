
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;

using SharedKernel;
namespace OpenIdentityStack.Application.Federation.Queries;
/// <summary>
/// Query to list all upstream providers.
/// </summary>
/// <param name="IncludeDisabled">Whether to include disabled providers.</param>
public sealed record ListProvidersQuery(bool IncludeDisabled = false);

/// <summary>
/// Data transfer object for provider information.
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
public sealed record ProviderDto(
    Guid Id,
    string Name,
    string DisplayName,
    string Authority,
    string ClientId,
    IReadOnlyList<string> Scopes,
    bool JitProvisioningEnabled,
    string Status,
    DateTimeOffset CreatedAt)
{
    public bool TrustEmailVerification { get; init; }
}

/// <summary>
/// Interface for the list providers query handler.
/// </summary>
public interface IListProvidersQueryHandler
{
    /// <summary>
    /// Handles the list providers query.
    /// </summary>
    /// <param name="query">The query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the list of providers or an error.</returns>
    Task<Result<IReadOnlyList<ProviderDto>>> HandleAsync(
        ListProvidersQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Query handler for listing upstream providers.
/// </summary>
public sealed class ListProvidersQueryHandler : IListProvidersQueryHandler
{
    private readonly IUpstreamProviderRepository providerRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListProvidersQueryHandler"/> class.
    /// </summary>
    public ListProvidersQueryHandler(IUpstreamProviderRepository providerRepository)
    {
        this.providerRepository = providerRepository;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ProviderDto>>> HandleAsync(
        ListProvidersQuery query,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<UpstreamProvider> providers = query.IncludeDisabled
            ? await this.providerRepository.GetAllAsync(cancellationToken).ConfigureAwait(false)
            : await this.providerRepository.GetActiveProvidersAsync(cancellationToken);

        var dtos = providers.Select(p => new ProviderDto(
            p.Id.Value,
            p.Name,
            p.DisplayName,
            p.Authority,
            p.ClientId,
            [], // Scopes would be stored in a separate configuration
            p.JitProvisioningEnabled,
            p.Status.ToString(),
            p.CreatedAt) { TrustEmailVerification = p.TrustEmailVerification }).ToList();

        return dtos;
    }
}
