using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Queries;

/// <summary>
/// Query to list upstream identities linked to a user.
/// </summary>
/// <param name="UserId">The user ID to list upstream identities for.</param>
public sealed record ListUserUpstreamIdentitiesQuery(UserId UserId);

/// <summary>
/// Represents a single upstream identity item in the list.
/// </summary>
/// <param name="ProviderId">The upstream provider ID.</param>
/// <param name="ProviderName">The provider name (e.g., "azure-ad", "google").</param>
/// <param name="SubjectId">The subject ID from the upstream provider.</param>
/// <param name="Email">The email from the upstream provider.</param>
/// <param name="LinkedAt">When the identity was linked.</param>
/// <param name="LastLoginAt">When the user last logged in via this identity.</param>
public sealed record UpstreamIdentityListItem(
    UpstreamProviderId ProviderId,
    string ProviderName,
    string SubjectId,
    string? Email,
    DateTimeOffset LinkedAt,
    DateTimeOffset? LastLoginAt,
    string? Issuer = null,
    string AssociationEvidence = "Unknown",
    bool IsQuarantined = true);

/// <summary>
/// Result of listing user upstream identities.
/// </summary>
/// <param name="Items">The list of upstream identities.</param>
public sealed record ListUserUpstreamIdentitiesResult(
    IReadOnlyList<UpstreamIdentityListItem> Items);

/// <summary>
/// Interface for the list user upstream identities query handler.
/// </summary>
public interface IListUserUpstreamIdentitiesQueryHandler
{
    /// <summary>
    /// Executes the query to list user upstream identities.
    /// </summary>
    Task<Result<ListUserUpstreamIdentitiesResult>> ExecuteAsync(
        ListUserUpstreamIdentitiesQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Handler for listing user upstream identities.
/// </summary>
public sealed class ListUserUpstreamIdentitiesQueryHandler : IListUserUpstreamIdentitiesQueryHandler
{
    private readonly IUserRepository userRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListUserUpstreamIdentitiesQueryHandler"/> class.
    /// </summary>
    public ListUserUpstreamIdentitiesQueryHandler(IUserRepository userRepository)
    {
        this.userRepository = userRepository;
    }

    /// <inheritdoc/>
    public async Task<Result<ListUserUpstreamIdentitiesResult>> ExecuteAsync(
        ListUserUpstreamIdentitiesQuery query,
        CancellationToken cancellationToken = default)
    {
        User? user = await this.userRepository.GetByIdAsync(query.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        var items = user.UpstreamIdentities
            .Select(identity => new UpstreamIdentityListItem(
                ProviderId: identity.ProviderId,
                ProviderName: identity.ProviderName,
                SubjectId: identity.SubjectId,
                Email: identity.Email,
                LinkedAt: identity.LinkedAt,
                LastLoginAt: identity.LastLoginAt,
                Issuer: identity.Issuer,
                AssociationEvidence: identity.AssociationEvidence.ToString(),
                IsQuarantined: identity.IsQuarantined))
            .ToList();

        return new ListUserUpstreamIdentitiesResult(items);
    }
}
