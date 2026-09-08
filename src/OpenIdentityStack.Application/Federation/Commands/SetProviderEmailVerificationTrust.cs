using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;
using SharedKernel;

namespace OpenIdentityStack.Application.Federation.Commands;

public sealed class SetProviderEmailVerificationTrust(IProviderEmailTrustStore store)
{
    public Task<Result> ExecuteAsync(Guid providerId, bool trusted, string actorId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Task.FromResult(Result.Failure(DomainError.Forbidden("ProviderTrust.ActorRequired", "An authenticated operator is required.")));
        }

        return store.SetAsync(UpstreamProviderId.From(providerId), trusted, actorId, cancellationToken);
    }
}
