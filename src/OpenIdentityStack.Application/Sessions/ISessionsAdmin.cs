using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Application.Sessions.Queries;
using SharedKernel;

namespace OpenIdentityStack.Application.Sessions;

/// <summary>
/// Session administration the API may call. Admin session use cases stay behind this type.
/// </summary>
public interface ISessionsAdmin
{
    Task<Result<ListSessionsResult>> ListAsync(ListSessionsQuery query, CancellationToken cancellationToken = default);

    Task<GetSessionResult?> GetAsync(GetSessionQuery query, CancellationToken cancellationToken = default);

    Task<Result<RevokeSessionResult>> RevokeAsync(RevokeSessionCommand command, CancellationToken cancellationToken = default);

    Task<Result<RevokeAllUserSessionsResult>> RevokeAllAsync(
        RevokeAllUserSessionsCommand command,
        CancellationToken cancellationToken = default);
}
