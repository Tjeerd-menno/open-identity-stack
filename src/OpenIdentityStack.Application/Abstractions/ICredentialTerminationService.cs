using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Atomically terminates a credential session and records the security action.
/// </summary>
public interface ICredentialTerminationService
{
    Task<Result> TerminateSessionAsync(
        SessionId sessionId,
        string actorId,
        string reason,
        bool isLogout = false,
        CancellationToken cancellationToken = default);

    Task<Result<int>> TerminateAllSessionsAsync(
        UserId userId,
        string actorId,
        string reason,
        SessionId? excludeSessionId = null,
        CancellationToken cancellationToken = default);
}
