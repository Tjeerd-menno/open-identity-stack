using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Sessions;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>
/// Retries pending back-channel logout delivery after request cancellation or restart.
/// </summary>
public sealed partial class PendingLogoutNotificationWorker : BackgroundService
{
    private static readonly TimeSpan pollingInterval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<PendingLogoutNotificationWorker> logger;

    [LoggerMessage(Level = LogLevel.Error, Message = "Unable to process pending logout notifications")]
    private partial void LogProcessingFailure(Exception exception);

    public PendingLogoutNotificationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PendingLogoutNotificationWorker> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(pollingInterval);
        do
        {
            try
            {
                await this.ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                this.LogProcessingFailure(ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = this.scopeFactory.CreateScope();
        ISessionRepository repository = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
        ILogoutNotifier notifier = scope.ServiceProvider.GetRequiredService<ILogoutNotifier>();
        IDateTimeProvider clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        IReadOnlyList<UserSession> sessions = await repository.GetTerminalSessionsWithPendingLogoutNotificationsAsync(
            clock.UtcNow,
            100,
            cancellationToken);

        foreach (UserSession session in sessions)
        {
            var clients = session.ClientSessions
                .Where(client => client.LogoutStatus is LogoutStatus.Pending or LogoutStatus.Failed)
                .Where(client => client.NextLogoutAttemptAt is null || client.NextLogoutAttemptAt <= clock.UtcNow)
                .Where(client => !string.IsNullOrWhiteSpace(client.BackChannelLogoutUri))
                .Select(client => new ClientSessionInfo(client.ClientId, client.FrontChannelLogoutUri, client.BackChannelLogoutUri))
                .ToList();

            if (clients.Count == 0)
            {
                continue;
            }

            LogoutNotificationResult result = await notifier.NotifyClientsAsync(session.Id, clients, cancellationToken);
            foreach (ClientSession client in session.ClientSessions.Where(client => clients.Any(sent => sent.ClientId == client.ClientId)))
            {
                if (result.FailedClients.Contains(client.ClientId, StringComparer.Ordinal))
                {
                    client.MarkLogoutAttemptFailed(clock);
                }
                else
                {
                    client.MarkLogoutCompleted(clock);
                }
            }

            await repository.UpdateAsync(session, cancellationToken);
        }
    }
}
