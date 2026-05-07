using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Sessions;

/// <summary>
/// Represents an authenticated user session.
/// A session tracks the user's login state across multiple client applications
/// and is used for session management and Single Logout (SLO).
/// </summary>
public sealed class UserSession : AggregateRoot<SessionId>
{
    private static readonly TimeSpan defaultSessionDuration = TimeSpan.FromHours(24);
    private readonly List<ClientSession> clientSessions = [];

    /// <summary>
    /// Gets the user ID who owns this session.
    /// </summary>
    public UserId UserId { get; private set; }

    /// <summary>
    /// Gets the IP address from which the session was created.
    /// </summary>
    public string IpAddress { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the user agent string from the session creation.
    /// </summary>
    public string UserAgent { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the current status of the session.
    /// </summary>
    public SessionStatus Status { get; private set; }

    /// <summary>
    /// Gets the last activity timestamp for this session.
    /// </summary>
    public DateTimeOffset LastActivityAt { get; private set; }

    /// <summary>
    /// Gets when the session was revoked (if applicable).
    /// </summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// Gets when the session expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// Gets the client sessions participating in this user session.
    /// </summary>
    public IReadOnlyList<ClientSession> ClientSessions => this.clientSessions.AsReadOnly();

    // EF Core constructor
    private UserSession() : base() { }

    private UserSession(
        SessionId id,
        UserId userId,
        string ipAddress,
        string userAgent,
        TimeSpan duration,
        DateTimeOffset createdAt) : base(id)
    {
        this.UserId = userId;
        this.IpAddress = ipAddress;
        this.UserAgent = userAgent;
        this.Status = SessionStatus.Active;
        this.LastActivityAt = createdAt;
        this.ExpiresAt = createdAt.Add(duration);
        this.RevokedAt = null;
        this.CreatedAt = createdAt;
    }

    /// <summary>
    /// Creates a new user session.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="ipAddress">The IP address.</param>
    /// <param name="userAgent">The user agent.</param>
    /// <param name="dateTimeProvider">The date time provider.</param>
    /// <param name="duration">Optional session duration. Defaults to 24 hours.</param>
    /// <returns>The created session or error.</returns>
    public static Result<UserSession> Create(
        UserId userId,
        string ipAddress,
        string userAgent,
        IDateTimeProvider dateTimeProvider,
        TimeSpan? duration = null)
    {
        if (userId.Value == Guid.Empty)
        {
            return SessionErrors.UserIdRequired;
        }

        var session = new UserSession(
            SessionId.Create(),
            userId,
            ipAddress ?? string.Empty,
            userAgent ?? string.Empty,
            duration ?? defaultSessionDuration,
            dateTimeProvider.UtcNow);

        return session;
    }

    /// <summary>
    /// Revokes this session, making it invalid for further use.
    /// </summary>
    public Result Revoke(IDateTimeProvider dateTimeProvider)
    {
        if (this.Status == SessionStatus.Revoked)
        {
            return SessionErrors.AlreadyRevoked;
        }

        if (this.Status == SessionStatus.Expired)
        {
            return SessionErrors.AlreadyExpired;
        }

        if (this.Status == SessionStatus.LoggedOut)
        {
            return SessionErrors.AlreadyLoggedOut;
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;
        this.Status = SessionStatus.Revoked;
        this.RevokedAt = now;

        this.RaiseDomainEvent(new SessionRevokedEvent(this.Id, this.UserId, now));

        return Result.Success();
    }

    /// <summary>
    /// Marks the session as logged out due to user-initiated logout.
    /// </summary>
    public Result Logout(IDateTimeProvider dateTimeProvider)
    {
        if (this.Status == SessionStatus.Revoked)
        {
            return SessionErrors.AlreadyRevoked;
        }

        if (this.Status == SessionStatus.Expired)
        {
            return SessionErrors.AlreadyExpired;
        }

        if (this.Status == SessionStatus.LoggedOut)
        {
            return SessionErrors.AlreadyLoggedOut;
        }

        this.Status = SessionStatus.LoggedOut;

        return Result.Success();
    }

    /// <summary>
    /// Marks the session as expired.
    /// </summary>
    public Result Expire(IDateTimeProvider dateTimeProvider)
    {
        if (this.Status == SessionStatus.Revoked)
        {
            return SessionErrors.AlreadyRevoked;
        }

        if (this.Status == SessionStatus.Expired)
        {
            return SessionErrors.AlreadyExpired;
        }

        if (this.Status == SessionStatus.LoggedOut)
        {
            return SessionErrors.AlreadyLoggedOut;
        }

        this.Status = SessionStatus.Expired;

        return Result.Success();
    }

    /// <summary>
    /// Checks if the session has expired.
    /// </summary>
    public bool IsExpired(IDateTimeProvider dateTimeProvider)
    {
        return dateTimeProvider.UtcNow > this.ExpiresAt;
    }

    /// <summary>
    /// Adds a client to this session (when user authenticates to a client app).
    /// </summary>
    public Result AddClientSession(string clientId, IDateTimeProvider dateTimeProvider)
    {
        if (this.Status != SessionStatus.Active)
        {
            return SessionErrors.SessionNotActive;
        }

        ClientSession? existingClient = this.clientSessions.FirstOrDefault(c => c.ClientId == clientId);
        if (existingClient is not null)
        {
            existingClient.UpdateLastAccess(dateTimeProvider);
            return Result.Success();
        }

        Result<ClientSession> clientSessionResult = ClientSession.Create(clientId, dateTimeProvider);
        if (clientSessionResult.IsFailure)
        {
            return clientSessionResult.Error;
        }

        this.clientSessions.Add(clientSessionResult.Value);
        return Result.Success();
    }

    /// <summary>
    /// Updates the last activity timestamp.
    /// </summary>
    public void UpdateLastActivity(IDateTimeProvider dateTimeProvider)
    {
        this.LastActivityAt = dateTimeProvider.UtcNow;
    }
}

/// <summary>
/// Domain event raised when a session is revoked.
/// </summary>
public sealed record SessionRevokedEvent(SessionId SessionId, UserId UserId, DateTimeOffset OccurredAt) 
    : DomainEvent(OccurredAt);
