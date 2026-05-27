using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Synchronizes application domain state to the OAuth/OIDC protocol store.
/// </summary>
public interface IApplicationProtocolProjection
{
    Task<Result> UpsertAsync(DomainApplication application, CancellationToken cancellationToken = default);

    Task<Result> UpsertAsync(DomainApplication application, string? clientSecret, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(DomainApplicationId applicationId, CancellationToken cancellationToken = default);
}
