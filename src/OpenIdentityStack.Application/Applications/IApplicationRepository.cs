using DomainApplication = OpenIdentityStack.Domain.Applications.Application;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications;

/// <summary>
/// Repository interface for application aggregate persistence.
/// </summary>
public interface IApplicationRepository
{
    Task<DomainApplication?> GetByIdAsync(DomainApplicationId id, CancellationToken cancellationToken = default);

    Task<DomainApplication?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByClientIdAsync(string clientId, CancellationToken cancellationToken = default);

    Task<(List<DomainApplication> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        OpenIdentityStack.Domain.Applications.ApplicationProfile? profile,
        OpenIdentityStack.Domain.Applications.ApplicationStatus? status,
        OpenIdentityStack.Domain.Applications.OAuthClientType? clientType,
        string? searchTerm,
        CancellationToken cancellationToken = default);

    Task AddAsync(DomainApplication application, CancellationToken cancellationToken = default);

    void Update(DomainApplication application);

    void Remove(DomainApplication application);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
