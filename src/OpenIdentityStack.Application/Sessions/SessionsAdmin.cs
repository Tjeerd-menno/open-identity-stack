using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Application.Sessions.Queries;
using SharedKernel;

namespace OpenIdentityStack.Application.Sessions;

internal sealed class SessionsAdmin : ISessionsAdmin
{
    private readonly IListSessionsQueryHandler listSessions;
    private readonly IGetSessionQueryHandler getSession;
    private readonly IRevokeSessionUseCase revokeSession;
    private readonly IRevokeAllUserSessionsUseCase revokeAll;

    internal SessionsAdmin(
        IListSessionsQueryHandler listSessions,
        IGetSessionQueryHandler getSession,
        IRevokeSessionUseCase revokeSession,
        IRevokeAllUserSessionsUseCase revokeAll)
    {
        this.listSessions = listSessions;
        this.getSession = getSession;
        this.revokeSession = revokeSession;
        this.revokeAll = revokeAll;
    }

    public Task<Result<ListSessionsResult>> ListAsync(ListSessionsQuery query, CancellationToken cancellationToken = default) =>
        this.listSessions.ExecuteAsync(query, cancellationToken);

    public Task<GetSessionResult?> GetAsync(GetSessionQuery query, CancellationToken cancellationToken = default) =>
        this.getSession.HandleAsync(query, cancellationToken);

    public Task<Result<RevokeSessionResult>> RevokeAsync(RevokeSessionCommand command, CancellationToken cancellationToken = default) =>
        this.revokeSession.ExecuteAsync(command, cancellationToken);

    public Task<Result<RevokeAllUserSessionsResult>> RevokeAllAsync(
        RevokeAllUserSessionsCommand command,
        CancellationToken cancellationToken = default) =>
        this.revokeAll.ExecuteAsync(command, cancellationToken);
}
