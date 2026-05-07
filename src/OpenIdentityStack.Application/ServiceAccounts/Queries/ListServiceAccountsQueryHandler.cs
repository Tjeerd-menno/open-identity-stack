using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Application.ServiceAccounts.Queries;

/// <summary>
/// Implementation of the query to list service accounts.
/// </summary>
public sealed class ListServiceAccountsQueryHandler : IListServiceAccountsQueryHandler
{
    private readonly IServiceAccountRepository serviceAccountRepository;

    public ListServiceAccountsQueryHandler(IServiceAccountRepository serviceAccountRepository)
    {
        this.serviceAccountRepository = serviceAccountRepository;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<ServiceAccountSummary>>> HandleAsync(
        int page = 1,
        int pageSize = 20,
        ServiceAccountStatus? status = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        (List<ServiceAccount>? items, int totalCount) = await this.serviceAccountRepository.ListAsync(
            page,
                pageSize,
                status,
                searchTerm,
                cancellationToken);

            var summaries = items.Select(s => new ServiceAccountSummary(
            s.Id,
            s.ClientId,
            s.DisplayName,
            s.Status,
            s.AllowedScopes,
            s.Credentials.Count,
            s.Certificates.Count,
            s.CreatedAt,
            s.ModifiedAt)).ToList();

        return PagedResult<ServiceAccountSummary>.Create(summaries, page, pageSize, totalCount);
    }
}
