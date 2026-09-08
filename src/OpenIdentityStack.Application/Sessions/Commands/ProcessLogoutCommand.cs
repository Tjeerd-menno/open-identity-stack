using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Application.Sessions.Commands;

/// <summary>
/// Result of processing a logout.
/// </summary>
/// <param name="SessionId">The terminated session ID.</param>
/// <param name="NotificationResult">Result of client notifications.</param>
/// <param name="FrontChannelLogoutUrls">URLs for front-channel logout iframes (if any).</param>
public sealed record ProcessLogoutResult(
    SessionId SessionId,
    LogoutNotificationResult NotificationResult,
    IReadOnlyList<string> FrontChannelLogoutUrls);

/// <summary>
/// Use case for processing user logout.
/// </summary>
public interface IProcessLogoutUseCase
{
    Task<Result<ProcessLogoutResult>> ExecuteAsync(
        SessionId sessionId,
        string? initiatingClientId,
        CancellationToken cancellationToken)
        => this.ExecuteAsync(sessionId, initiatingClientId, "system", cancellationToken);

    Task<Result<ProcessLogoutResult>> ExecuteAsync(
        SessionId sessionId,
        string? initiatingClientId = null,
        string actorId = "system",
        CancellationToken cancellationToken = default);
}

public sealed class ProcessLogoutUseCase : IProcessLogoutUseCase
{
    private readonly ISessionRepository sessionRepository;
    private readonly ILogoutNotifier logoutNotifier;
    private readonly IFrontChannelLogoutService frontChannelLogoutService;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly ICredentialTerminationService credentialTerminationService;

    public ProcessLogoutUseCase(
        ISessionRepository sessionRepository,
        ILogoutNotifier logoutNotifier,
        IFrontChannelLogoutService frontChannelLogoutService,
        IDateTimeProvider dateTimeProvider,
        ICredentialTerminationService credentialTerminationService)
    {
        this.sessionRepository = sessionRepository;
        this.logoutNotifier = logoutNotifier;
        this.frontChannelLogoutService = frontChannelLogoutService;
        this.dateTimeProvider = dateTimeProvider;
        this.credentialTerminationService = credentialTerminationService;
    }

    public async Task<Result<ProcessLogoutResult>> ExecuteAsync(
        SessionId sessionId,
        string? initiatingClientId = null,
        string actorId = "system",
        CancellationToken cancellationToken = default)
    {
        UserSession? session = await this.sessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return DomainError.NotFound("Session.NotFound", $"Session {sessionId.Value} not found");
        }

        Result terminationResult = await this.credentialTerminationService.TerminateSessionAsync(
            sessionId,
            actorId,
            "logout",
            true,
            cancellationToken);
        if (terminationResult.IsFailure)
        {
            return terminationResult.Error;
        }

        // The termination service uses its own atomic persistence boundary. Reload the
        // aggregate before recording delivery outcomes so this use case never writes a
        // stale active session over the terminal state.
        session = await this.sessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return DomainError.NotFound("Session.NotFound", $"Session {sessionId.Value} not found");
        }

        // Get clients to notify (excluding the initiating client)
        var participatingClients = session.ClientSessions
            .Where(cs => cs.ClientId != initiatingClientId)
            .Select(cs => new ClientSessionInfo(
                cs.ClientId,
                cs.FrontChannelLogoutUri,
                cs.BackChannelLogoutUri))
            .ToList();

        var clientsToNotify = session.ClientSessions
            .Where(cs => cs.ClientId != initiatingClientId)
            .Where(cs => cs.LogoutStatus is LogoutStatus.Pending or LogoutStatus.Failed)
            .Where(cs => cs.NextLogoutAttemptAt is null || cs.NextLogoutAttemptAt <= this.dateTimeProvider.UtcNow)
            .Select(cs => new ClientSessionInfo(
                cs.ClientId,
                cs.FrontChannelLogoutUri,
                cs.BackChannelLogoutUri))
            .ToList();

        LogoutNotificationResult notificationResult;
        IReadOnlyList<string> frontChannelUrls = [];

        if (clientsToNotify.Count > 0)
        {
            // Notify clients of logout
            notificationResult = await this.logoutNotifier.NotifyClientsAsync(
                sessionId,
                clientsToNotify,
                cancellationToken);

            foreach (ClientSession clientSession in session.ClientSessions
                         .Where(clientSession => clientsToNotify.Any(client => client.ClientId == clientSession.ClientId)))
            {
                if (notificationResult.FailedClients.Contains(clientSession.ClientId, StringComparer.Ordinal))
                {
                    clientSession.MarkLogoutAttemptFailed(this.dateTimeProvider);
                }
                else
                {
                    clientSession.MarkLogoutCompleted(this.dateTimeProvider);
                }
            }

            await this.sessionRepository.UpdateAsync(session, cancellationToken);

        }
        else
        {
            notificationResult = new LogoutNotificationResult(0, 0, []);
        }

        frontChannelUrls = this.frontChannelLogoutService
            .GenerateLogoutFrames(sessionId, participatingClients)
            .Select(frame => frame.IframeUrl)
            .ToList();

        return new ProcessLogoutResult(sessionId, notificationResult, frontChannelUrls);
    }
}
