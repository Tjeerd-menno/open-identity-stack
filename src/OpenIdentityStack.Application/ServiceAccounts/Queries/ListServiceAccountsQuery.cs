using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Application.ServiceAccounts.Queries;

/// <summary>
/// Summary of a service account for listing.
/// </summary>
/// <param name="Id">The service account ID.</param>
/// <param name="ClientId">The OAuth2 client_id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Status">The current status.</param>
/// <param name="AllowedScopes">The allowed scopes.</param>
/// <param name="CredentialCount">Number of credentials.</param>
/// <param name="CertificateCount">Number of certificates.</param>
/// <param name="CreatedAt">When the account was created.</param>
/// <param name="ModifiedAt">When the account was last modified.</param>
public sealed record ServiceAccountSummary(
    ServiceAccountId Id,
    string ClientId,
    string DisplayName,
    ServiceAccountStatus Status,
    IReadOnlyList<string> AllowedScopes,
    int CredentialCount,
    int CertificateCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

/// <summary>
/// Handler interface for ListServiceAccountsQuery.
/// </summary>
public interface IListServiceAccountsQueryHandler
{
    /// <summary>
    /// Lists service accounts with pagination.
    /// </summary>
    Task<Result<PagedResult<ServiceAccountSummary>>> HandleAsync(
        int page = 1,
        int pageSize = 20,
        ServiceAccountStatus? status = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default);
}
