using Microsoft.AspNetCore.Mvc;
using OpenIdentityStack.Api.Common;
using OpenIddict.Validation.AspNetCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Application.Sessions.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Api.Sessions;

/// <summary>
/// Minimal API endpoints for managing user sessions via the Admin API.
/// </summary>
internal static class SessionsApi
{
    public static IEndpointRouteBuilder MapSessionsApi(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/admin/sessions")
            .WithTags(nameof(SessionsApi));

        group.MapGet(string.Empty, ListSessions)
            .RequireAuthorization(Permissions.Sessions.Read)
            .Produces<SessionListResponse>(StatusCodes.Status200OK)
            .WithName("ListSessions")
            .WithSummary("Lists sessions with optional filtering");

        group.MapGet("{id:guid}", GetSession)
            .RequireAuthorization(Permissions.Sessions.Read)
            .Produces<SessionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetSession")
            .WithSummary("Gets a session by ID");

        group.MapDelete("{id:guid}", RevokeSession)
            .RequireAuthorization(Permissions.Sessions.Revoke)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("RevokeSession")
            .WithSummary("Revokes a session");

        // User-specific session operations (using alternate route pattern)
        app.MapDelete("api/admin/users/{userId:guid}/sessions", RevokeAllUserSessions)
            .RequireAuthorization(Permissions.Sessions.Revoke)
            .Produces<RevokeAllSessionsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("RevokeAllUserSessions")
            .WithTags(nameof(SessionsApi))
            .WithSummary("Revokes all sessions for a user");

        return app;
    }

    private static async Task<IResult> ListSessions(
        [FromServices] IListSessionsQueryHandler listSessionsQueryHandler,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? status = null)
    {
        SessionStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<SessionStatus>(status, true, out SessionStatus parsed))
        {
            statusFilter = parsed;
        }

        var query = new ListSessionsQuery(
            page,
            pageSize,
            userId.HasValue ? new UserId(userId.Value) : null,
            statusFilter);

        Result<ListSessionsResult> result = await listSessionsQueryHandler.ExecuteAsync(query);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        var response = new SessionListResponse(
            result.Value.Items.Select(s => new SessionResponse(
                s.Id.Value,
                s.UserId.Value,
                s.IpAddress,
                s.UserAgent,
                s.Status.ToString(),
                s.ClientCount,
                s.LastActivityAt,
                s.ExpiresAt,
                s.CreatedAt)).ToList(),
            result.Value.TotalCount,
            result.Value.Page,
            result.Value.PageSize,
            result.Value.TotalPages);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> GetSession(
        [FromServices] ISessionRepository sessionRepository,
        Guid id)
    {
        UserSession? session = await sessionRepository.GetByIdAsync(new SessionId(id));
        if (session is null)
        {
            return TypedResults.NotFound(new { error = "Session not found." });
        }

        var response = new SessionResponse(
            session.Id.Value,
            session.UserId.Value,
            session.IpAddress,
            session.UserAgent,
            session.Status.ToString(),
            session.ClientSessions.Count,
            session.LastActivityAt,
            session.ExpiresAt,
            session.CreatedAt);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> RevokeSession(
        [FromServices] IRevokeSessionUseCase revokeSessionUseCase,
        Guid id)
    {
        var command = new RevokeSessionCommand(new SessionId(id));
        Result<RevokeSessionResult> result = await revokeSessionUseCase.ExecuteAsync(command);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> RevokeAllUserSessions(
        [FromServices] IRevokeAllUserSessionsUseCase revokeAllUserSessionsUseCase,
        Guid userId,
        [FromQuery] Guid? excludeSessionId = null)
    {
        var command = new RevokeAllUserSessionsCommand(
            new UserId(userId),
            excludeSessionId.HasValue ? new SessionId(excludeSessionId.Value) : null);

        Result<RevokeAllUserSessionsResult> result = await revokeAllUserSessionsUseCase.ExecuteAsync(command);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        var response = new RevokeAllSessionsResponse(
            result.Value.RevokedCount,
            result.Value.RevokedAt);

        return TypedResults.Ok(response);
    }

}
