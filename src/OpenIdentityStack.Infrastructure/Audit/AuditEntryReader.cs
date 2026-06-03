using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Audit.Queries;
using OpenIdentityStack.Infrastructure.Persistence;

namespace OpenIdentityStack.Infrastructure.Audit;

public sealed class AuditEntryReader : IAuditEntryReader
{
    private readonly OpenIdentityStackDbContext dbContext;

    public AuditEntryReader(OpenIdentityStackDbContext dbContext)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ListAuditEntriesResult> ListAsync(
        ListAuditEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<AuditLogEntry> entries = this.dbContext.AuditLogEntries.AsNoTracking();

        entries = ApplyFilters(entries, query);

        int totalCount = await entries.CountAsync(cancellationToken);
        List<AuditEntryListItem> items = await entries
            .OrderByDescending(entry => entry.Timestamp)
            .ThenByDescending(entry => entry.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(entry => new AuditEntryListItem(
                entry.Id,
                entry.Timestamp,
                entry.UserId,
                entry.Action,
                entry.EntityType,
                entry.EntityId,
                entry.Details,
                entry.BeforeState,
                entry.AfterState))
            .ToListAsync(cancellationToken);

        return new ListAuditEntriesResult(items, totalCount, query.Page, query.PageSize);
    }

    private static IQueryable<AuditLogEntry> ApplyFilters(
        IQueryable<AuditLogEntry> entries,
        ListAuditEntriesQuery query)
    {
        if (query.From is not null)
        {
            entries = entries.Where(entry => entry.Timestamp >= query.From.Value);
        }

        if (query.To is not null)
        {
            entries = entries.Where(entry => entry.Timestamp <= query.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.UserId))
        {
            entries = entries.Where(entry => entry.UserId == query.UserId);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            entries = entries.Where(entry => entry.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            entries = entries.Where(entry => entry.EntityType == query.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            entries = entries.Where(entry => entry.EntityId == query.EntityId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search;
            entries = entries.Where(entry =>
                entry.UserId.Contains(search)
                || entry.Action.Contains(search)
                || entry.EntityType.Contains(search)
                || entry.EntityId.Contains(search)
                || (entry.Details != null && entry.Details.Contains(search))
                || (entry.BeforeState != null && entry.BeforeState.Contains(search))
                || (entry.AfterState != null && entry.AfterState.Contains(search)));
        }

        return entries;
    }
}
