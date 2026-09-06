using OpenIdentityStack.Application.Abstractions;
using SharedKernel;

namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Rejects identifier-only linking until a proof-based account linking flow exists.
/// </summary>
public sealed class LinkUpstreamIdentityUseCase(IAuditLog auditLog) : ILinkUpstreamIdentityUseCase
{
    public async Task<Result<LinkUpstreamIdentityResult>> ExecuteAsync(
        LinkUpstreamIdentityCommand command,
        CancellationToken cancellationToken = default)
    {
        await auditLog.LogAsync(
            command.ActorId,
            "User.UpstreamIdentityLinkDenied",
            "User",
            command.UserId.Value.ToString(),
            "Independent account-control proof required.",
            cancellationToken);

        return DomainError.Forbidden(
            "UpstreamIdentity.ProofRequired",
            "Linking an existing account requires proof of account control.");
    }
}
