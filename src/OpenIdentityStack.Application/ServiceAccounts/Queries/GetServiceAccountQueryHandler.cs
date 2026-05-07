using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Application.ServiceAccounts.Queries;

/// <summary>
/// Implementation of the query to get a service account.
/// </summary>
public sealed class GetServiceAccountQueryHandler : IGetServiceAccountQueryHandler
{
    private readonly IServiceAccountRepository serviceAccountRepository;

    public GetServiceAccountQueryHandler(IServiceAccountRepository serviceAccountRepository)
    {
        this.serviceAccountRepository = serviceAccountRepository;
    }

    /// <inheritdoc />
    public async Task<Result<ServiceAccountDetails>> HandleAsync(
        ServiceAccountId id,
        CancellationToken cancellationToken = default)
    {
        ServiceAccount? serviceAccount = await this.serviceAccountRepository.GetByIdAsync(id, cancellationToken);

        if (serviceAccount is null)
        {
            return ServiceAccountErrors.NotFound;
        }

        return new ServiceAccountDetails(
            serviceAccount.Id,
            serviceAccount.ClientId,
            serviceAccount.DisplayName,
            serviceAccount.Status,
            serviceAccount.AllowedScopes,
            serviceAccount.AllowedGrantTypes,
            serviceAccount.Credentials.Count,
            serviceAccount.Certificates.Count,
            serviceAccount.CreatedAt,
            serviceAccount.ModifiedAt);
    }
}
