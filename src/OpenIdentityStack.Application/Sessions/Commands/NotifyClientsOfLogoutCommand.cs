using OpenIdentityStack.Application.Abstractions;

using SharedKernel;
namespace OpenIdentityStack.Application.Sessions.Commands;

/// <summary>
/// Use case for notifying clients about a user logout (SLO notification).
/// </summary>
public interface INotifyClientsOfLogoutUseCase
{
    Task<Result<LogoutNotificationResult>> ExecuteAsync(
        SessionId sessionId,
        IReadOnlyList<ClientSessionInfo> clients,
        CancellationToken cancellationToken = default);
}

public sealed class NotifyClientsOfLogoutUseCase : INotifyClientsOfLogoutUseCase
{
    private readonly ILogoutNotifier logoutNotifier;

    public NotifyClientsOfLogoutUseCase(ILogoutNotifier logoutNotifier)
    {
        this.logoutNotifier = logoutNotifier;
    }

    public async Task<Result<LogoutNotificationResult>> ExecuteAsync(
        SessionId sessionId,
        IReadOnlyList<ClientSessionInfo> clients,
        CancellationToken cancellationToken = default)
    {
        if (clients.Count == 0)
        {
            return new LogoutNotificationResult(0, 0, []);
        }

        LogoutNotificationResult result = await this.logoutNotifier.NotifyClientsAsync(
            sessionId,
            clients,
            cancellationToken);

        return result;
    }
}
