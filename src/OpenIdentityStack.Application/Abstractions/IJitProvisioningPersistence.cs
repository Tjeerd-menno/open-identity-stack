using OpenIdentityStack.Domain.Federation;
using SharedKernel;

namespace OpenIdentityStack.Application.Abstractions;

/// <summary>Commits tracked provisioning changes and association audit atomically; expected identity conflicts fail closed.</summary>
public interface IJitProvisioningPersistence
{
    Task<Result> CommitAsync(UserId userId, UpstreamProviderId providerId, bool isNewUser, CancellationToken cancellationToken = default);
}
