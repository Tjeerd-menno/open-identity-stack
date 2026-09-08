using OpenIdentityStack.Domain.Federation;
using SharedKernel;

namespace OpenIdentityStack.Application.Abstractions;

/// <summary>Atomically persists provider trust, evidence withdrawal, and its audit record.</summary>
public interface IProviderEmailTrustStore
{
    Task<Result> SetAsync(UpstreamProviderId providerId, bool trusted, string actorId, CancellationToken cancellationToken);
}
