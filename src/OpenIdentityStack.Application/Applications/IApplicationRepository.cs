using OpenIdentityStack.Domain.Applications;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications;

/// <summary>
/// Client fields read on the token path. The aggregate and its credentials stay unloaded.
/// </summary>
internal sealed record ClientTokenProjection(
    DomainApplicationId Id,
    ApplicationStatus Status,
    IReadOnlyList<string> AllowedScopes);

/// <summary>
/// Repository interface for application aggregate persistence.
/// </summary>
internal interface IApplicationRepository
{
    Task<DomainApplication?> GetByIdAsync(DomainApplicationId id, CancellationToken cancellationToken = default);

    Task<DomainApplication?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);

    Task<ClientTokenProjection?> GetTokenProjectionAsync(string clientId, CancellationToken cancellationToken = default);

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
