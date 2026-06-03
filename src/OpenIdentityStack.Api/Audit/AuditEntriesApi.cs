using Microsoft.AspNetCore.Mvc;
using OpenIdentityStack.Api.Common;
using OpenIdentityStack.Application.Audit.Queries;
using OpenIdentityStack.Application.Authorization;

using SharedKernel;

namespace OpenIdentityStack.Api.Audit;

internal static class AuditEntriesApi
{
    public static IEndpointRouteBuilder MapAuditEntriesApi(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/admin/audit-entries")
            .WithTags(nameof(AuditEntriesApi));

        group.MapGet(string.Empty, ListAuditEntries)
            .RequireAuthorization(Permissions.AuditLogs.Read)
            .Produces<AuditEntryListResponse>(StatusCodes.Status200OK)
            .WithName("ListAuditEntries")
            .WithSummary("Lists audit entries with optional filtering");

        return app;
    }

    private static async Task<IResult> ListAuditEntries(
        [FromServices] IListAuditEntriesQueryHandler listAuditEntriesQueryHandler,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? search = null)
    {
        ListAuditEntriesQuery query = new(
            page,
            pageSize,
            from,
            to,
            userId,
            action,
            entityType,
            entityId,
            search);

        Result<ListAuditEntriesResult> result = await listAuditEntriesQueryHandler.ExecuteAsync(query);
        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        AuditEntryListResponse response = new(
            result.Value.Items.Select(entry => new AuditEntryResponse(
                entry.Id,
                entry.Timestamp,
                entry.UserId,
                entry.Action,
                entry.EntityType,
                entry.EntityId,
                entry.Details,
                entry.BeforeState,
                entry.AfterState)).ToList(),
            result.Value.TotalCount,
            result.Value.Page,
            result.Value.PageSize,
            result.Value.TotalPages,
            result.Value.HasPreviousPage,
            result.Value.HasNextPage);

        return TypedResults.Ok(response);
    }
}
