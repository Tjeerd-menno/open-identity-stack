using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Sessions;

namespace OpenIdentityStack.Application.Sessions.Queries;

/// <summary>
/// Query to load one session by id.
/// </summary>
/// <param name="SessionId">The session id.</param>
public sealed record GetSessionQuery(SessionId SessionId);

/// <summary>
/// Session fields returned by the admin get-session endpoint.
/// </summary>
public sealed record GetSessionResult(
    SessionId Id,
    UserId UserId,
    string IpAddress,
    string UserAgent,
    SessionStatus Status,
    int ClientCount,
    DateTimeOffset LastActivityAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);

/// <summary>
/// Query handler for loading one session.
/// </summary>
internal interface IGetSessionQueryHandler
{
    /// <summary>
    /// Returns the session, or null when it does not exist.
    /// </summary>
    Task<GetSessionResult?> HandleAsync(
        GetSessionQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Loads a session through the session repository.
/// </summary>
internal sealed class GetSessionQueryHandler : IGetSessionQueryHandler
{
    private readonly ISessionRepository sessionRepository;

    public GetSessionQueryHandler(ISessionRepository sessionRepository)
    {
        this.sessionRepository = sessionRepository;
    }

    /// <inheritdoc />
    public async Task<GetSessionResult?> HandleAsync(
        GetSessionQuery query,
        CancellationToken cancellationToken = default)
    {
        UserSession? session = await this.sessionRepository.GetByIdAsync(query.SessionId, cancellationToken);
        if (session is null)
        {
            return null;
        }

        return new GetSessionResult(
            session.Id,
            session.UserId,
            session.IpAddress,
            session.UserAgent,
            session.Status,
            session.ClientSessions.Count,
            session.LastActivityAt,
            session.ExpiresAt,
            session.CreatedAt);
    }
}
