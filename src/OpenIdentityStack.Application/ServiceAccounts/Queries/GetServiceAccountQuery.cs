using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Application.ServiceAccounts.Queries;

/// <summary>
/// Detailed view of a service account.
/// </summary>
public sealed record ServiceAccountDetails(
    ServiceAccountId Id,
    string ClientId,
    string DisplayName,
    ServiceAccountStatus Status,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> AllowedGrantTypes,
    int CredentialCount,
    int CertificateCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

/// <summary>
/// Handler interface for GetServiceAccountQuery.
/// </summary>
public interface IGetServiceAccountQueryHandler
{
    /// <summary>
    /// Executes the query.
    /// </summary>
    Task<Result<ServiceAccountDetails>> HandleAsync(ServiceAccountId id, CancellationToken cancellationToken = default);
}
