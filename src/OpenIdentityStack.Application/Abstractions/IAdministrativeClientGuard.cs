using SharedKernel;
using ApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Abstractions;

/// <summary>Protects credentials and OAuth configuration that could transfer an approved client's access.</summary>
public interface IAdministrativeClientGuard
{
    Task<Result> RequireAsync(ApplicationId applicationId, string operation, CancellationToken cancellationToken = default);
    Task RecordOutcomeAsync(CancellationToken cancellationToken = default);
}
