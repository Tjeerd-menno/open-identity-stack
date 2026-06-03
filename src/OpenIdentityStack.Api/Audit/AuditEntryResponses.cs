namespace OpenIdentityStack.Api.Audit;

public sealed record AuditEntryResponse(
    Guid Id,
    DateTimeOffset Timestamp,
    string UserId,
    string Action,
    string EntityType,
    string EntityId,
    string? Details,
    string? BeforeState,
    string? AfterState);

public sealed record AuditEntryListResponse(
    IReadOnlyList<AuditEntryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);
