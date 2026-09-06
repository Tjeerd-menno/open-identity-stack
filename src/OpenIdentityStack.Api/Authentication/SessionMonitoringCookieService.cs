using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;
using SharedKernel;

namespace OpenIdentityStack.Api.Authentication;

public interface ISessionMonitoringCookieService
{
    string Create(UserId userId, SessionId sessionId, Guid credentialEpoch, DateTimeOffset expiresUtc);

    Task<bool> IsCurrentAsync(string? value, CancellationToken cancellationToken = default);
}

public sealed class SessionMonitoringCookieService : ISessionMonitoringCookieService
{
    private const int currentVersion = 1;
    private const string protectorPurpose = "OpenIdentityStack.SessionManagement.op_session.v1";
    private readonly IDataProtector protector;
    private readonly IUserRepository users;
    private readonly ISessionRepository sessions;
    private readonly ICredentialBoundaryStore boundary;
    private readonly IDateTimeProvider clock;

    public SessionMonitoringCookieService(
        IDataProtectionProvider dataProtection,
        IUserRepository users,
        ISessionRepository sessions,
        ICredentialBoundaryStore boundary,
        IDateTimeProvider clock)
    {
        this.protector = dataProtection.CreateProtector(protectorPurpose);
        this.users = users;
        this.sessions = sessions;
        this.boundary = boundary;
        this.clock = clock;
    }

    public string Create(UserId userId, SessionId sessionId, Guid credentialEpoch, DateTimeOffset expiresUtc)
    {
        var payload = new SessionMonitoringCookiePayload(
            currentVersion, userId.Value, sessionId.Value, credentialEpoch, expiresUtc);
        return this.protector.Protect(JsonSerializer.Serialize(payload));
    }

    public async Task<bool> IsCurrentAsync(string? value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        SessionMonitoringCookiePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SessionMonitoringCookiePayload>(this.protector.Unprotect(value));
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            return IsLegacyCookie(value)
                && await this.boundary.IsCurrentAsync(null, cancellationToken);
        }

        if (payload is null
            || payload.Version != currentVersion
            || payload.UserId == Guid.Empty
            || payload.SessionId == Guid.Empty
            || payload.ExpiresUtc <= this.clock.UtcNow
            || !await this.boundary.IsCurrentAsync(payload.CredentialEpoch.ToString(), cancellationToken))
        {
            return false;
        }

        var userId = new UserId(payload.UserId);
        var sessionId = new SessionId(payload.SessionId);
        Domain.Users.User? user = await this.users.GetByIdAsync(userId, cancellationToken);
        UserSession? session = await this.sessions.GetByIdAsync(sessionId, cancellationToken);
        return user?.CanAuthenticate() == true
            && session?.UserId == userId
            && session.Status == SessionStatus.Active
            && !session.IsExpired(this.clock);
    }

    private static bool IsLegacyCookie(string value)
    {
        try
        {
            return WebEncoders.Base64UrlDecode(value).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private sealed record SessionMonitoringCookiePayload(
        int Version,
        Guid UserId,
        Guid SessionId,
        Guid CredentialEpoch,
        DateTimeOffset ExpiresUtc);
}
