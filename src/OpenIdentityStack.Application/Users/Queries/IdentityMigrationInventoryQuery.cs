using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Application.Users.Queries;

/// <summary>Read-only migration inventory; candidates are not proof of account control.</summary>
public sealed record IdentityMigrationInventoryQuery(int Page = 1, int PageSize = 20, Guid? ProviderId = null);

public sealed record IdentityMigrationLink(Guid ProviderId, string ProviderName, string SubjectId, string? Issuer, string AssociationEvidence, bool IsQuarantined);

public sealed record IdentityMigrationUser(Guid UserId, string DisplayName, string Status, bool HasPasswordCredential, IReadOnlyList<Guid> CandidateFederationProviderIds, bool MigrationBlocked, bool RecoveryRequired, IReadOnlyList<IdentityMigrationLink> Identities);

public sealed record IdentityMigrationInventoryResult(IReadOnlyList<IdentityMigrationUser> Items, int TotalCount, int Page, int PageSize);

public interface IIdentityMigrationInventoryQueryHandler
{
    Task<Result<IdentityMigrationInventoryResult>> ExecuteAsync(IdentityMigrationInventoryQuery query, CancellationToken cancellationToken = default);
}

public sealed class IdentityMigrationInventoryQueryHandler(IUserRepository users, IUpstreamProviderRepository providers) : IIdentityMigrationInventoryQueryHandler
{
    public async Task<Result<IdentityMigrationInventoryResult>> ExecuteAsync(IdentityMigrationInventoryQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > 100 || query.Page > int.MaxValue / query.PageSize)
        {
            return DomainError.Validation("IdentityInventory.InvalidPagination", "Page must be positive and page size must be between 1 and 100.");
        }

        UpstreamProviderId? providerId = query.ProviderId is Guid id ? UpstreamProviderId.From(id) : null;
        (IReadOnlyList<User> items, int totalCount) = await users.ListWithUpstreamIdentitiesAsync(query.Page, query.PageSize, providerId, cancellationToken);
        IReadOnlyList<UpstreamProvider> activeProviders = await providers.GetActiveProvidersAsync(cancellationToken);
        var inventory = items.Select(user =>
        {
            bool blocked = user.UpstreamIdentities.Any(identity => identity.IsQuarantined);
            bool enabled = user.Status != UserStatus.Disabled;
            Guid[] candidateProviders = user.UpstreamIdentities
                .Where(identity => enabled && !identity.IsQuarantined && activeProviders.Any(provider => provider.Id == identity.ProviderId && provider.BoundIssuer == identity.Issuer))
                .Select(identity => identity.ProviderId.Value).ToArray();
            bool passwordConfigured = user.HasPassword();
            return new IdentityMigrationUser(user.Id.Value, user.DisplayName, user.Status.ToString(), passwordConfigured, candidateProviders, blocked,
                blocked && (!enabled || (!passwordConfigured && candidateProviders.Length == 0)),
                user.UpstreamIdentities.OrderBy(identity => identity.ProviderId.Value).Select(identity => new IdentityMigrationLink(identity.ProviderId.Value, identity.ProviderName, identity.SubjectId, identity.Issuer, identity.AssociationEvidence.ToString(), identity.IsQuarantined)).ToList());
        }).ToList();
        return new IdentityMigrationInventoryResult(inventory, totalCount, query.Page, query.PageSize);
    }
}
