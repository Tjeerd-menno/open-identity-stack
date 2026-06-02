using OpenIdentityStack.Application.Abstractions;

using SharedKernel;

namespace OpenIdentityStack.Application.Audit.Queries;

public sealed record ListAuditEntriesQuery(
    int Page = 1,
    int PageSize = 20,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? UserId = null,
    string? Action = null,
    string? EntityType = null,
    string? EntityId = null,
    string? Search = null);

public sealed record AuditEntryListItem(
    Guid Id,
    DateTimeOffset Timestamp,
    string UserId,
    string Action,
    string EntityType,
    string EntityId,
    string? Details,
    string? BeforeState,
    string? AfterState);

public sealed record ListAuditEntriesResult(
    IReadOnlyList<AuditEntryListItem> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => this.PageSize <= 0 ? 0 : (int)Math.Ceiling((double)this.TotalCount / this.PageSize);

    public bool HasNextPage => this.Page < this.TotalPages;

    public bool HasPreviousPage => this.Page > 1;
}

public interface IListAuditEntriesQueryHandler
{
    Task<Result<ListAuditEntriesResult>> ExecuteAsync(
        ListAuditEntriesQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class ListAuditEntriesQueryHandler : IListAuditEntriesQueryHandler
{
    private readonly IAuditEntryReader auditEntryReader;

    public ListAuditEntriesQueryHandler(IAuditEntryReader auditEntryReader)
    {
        this.auditEntryReader = auditEntryReader ?? throw new ArgumentNullException(nameof(auditEntryReader));
    }

    public async Task<Result<ListAuditEntriesResult>> ExecuteAsync(
        ListAuditEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        ListAuditEntriesQuery normalized = query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, 100)
        };

        ListAuditEntriesResult result = await this.auditEntryReader.ListAsync(normalized, cancellationToken);
        return result;
    }
}
