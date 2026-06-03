using OpenIdentityStack.Application.Audit.Queries;

namespace OpenIdentityStack.Application.Abstractions;

public interface IAuditEntryReader
{
    Task<ListAuditEntriesResult> ListAsync(
        ListAuditEntriesQuery query,
        CancellationToken cancellationToken = default);
}
