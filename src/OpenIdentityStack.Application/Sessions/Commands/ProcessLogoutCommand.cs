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
        string? initiatingClientId = null,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessLogoutUseCase : IProcessLogoutUseCase
{
    private readonly ISessionRepository sessionRepository;
    private readonly ILogoutNotifier logoutNotifier;
    private readonly IDateTimeProvider dateTimeProvider;

    public ProcessLogoutUseCase(
        ISessionRepository sessionRepository,
        ILogoutNotifier logoutNotifier,
        IDateTimeProvider dateTimeProvider)
    {
        this.sessionRepository = sessionRepository;
        this.logoutNotifier = logoutNotifier;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<ProcessLogoutResult>> ExecuteAsync(
        SessionId sessionId,
        string? initiatingClientId = null,
        CancellationToken cancellationToken = default)
    {
        UserSession? session = await this.sessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return DomainError.NotFound("Session.NotFound", $"Session {sessionId.Value} not found");
        }

        if (session.Status == SessionStatus.Revoked)
        {
            return DomainError.Conflict("Session.AlreadyRevoked", "Session has already been revoked");
        }

        if (session.Status == SessionStatus.LoggedOut)
        {
            return DomainError.Conflict("Session.AlreadyLoggedOut", "Session has already been logged out");
        }

        // Logout the session
        Result logoutResult = session.Logout(this.dateTimeProvider);
        if (logoutResult.IsFailure)
        {
            return logoutResult.Error;
        }

        await this.sessionRepository.UpdateAsync(session, cancellationToken);

        // Get clients to notify (excluding the initiating client)
        var clientsToNotify = session.ClientSessions
            .Where(cs => cs.ClientId != initiatingClientId)
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

            // Generate front-channel logout URLs
            frontChannelUrls = this.logoutNotifier.GenerateFrontChannelLogoutUrls(
                sessionId,
                clientsToNotify);
        }
        else
        {
            notificationResult = new LogoutNotificationResult(0, 0, []);
        }

        return new ProcessLogoutResult(sessionId, notificationResult, frontChannelUrls);
    }
}
