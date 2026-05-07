using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Infrastructure.Persistence;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Audit;

/// <summary>
/// Implementation of <see cref="IAuditLog"/> that logs to a logger and optionally to the database.
/// </summary>
public sealed partial class AuditLogService : IAuditLog
{
    private readonly ILogger<AuditLogService> logger;
    private readonly OpenIdentityStackDbContext dbContext;
    private readonly IDateTimeProvider dateTimeProvider;

    public AuditLogService(
        ILogger<AuditLogService> logger,
        OpenIdentityStackDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        this.logger = logger;
        this.dbContext = dbContext;
        this.dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task LogAsync(
        string userId,
        string action,
        string entityType,
        string entityId,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset timestamp = this.dateTimeProvider.UtcNow;
        LogAuditAction(this.logger, userId, action, entityType, entityId, timestamp, details ?? "N/A");

        this.dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            Timestamp = timestamp
        });

        await this.dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task LogChangeAsync(
        string userId,
        string action,
        string entityType,
        string entityId,
        string? beforeState,
        string? afterState,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset timestamp = this.dateTimeProvider.UtcNow;
        LogAuditChange(this.logger, userId, action, entityType, entityId, timestamp, beforeState ?? "N/A", afterState ?? "N/A");

        this.dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            BeforeState = beforeState,
            AfterState = afterState,
            Timestamp = timestamp
        });

        await this.dbContext.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Audit: User {UserId} performed {Action} on {EntityType} {EntityId} at {Timestamp}. Details: {Details}")]
    private static partial void LogAuditAction(
        ILogger logger,
        string userId,
        string action,
        string entityType,
        string entityId,
        DateTimeOffset timestamp,
        string details);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Audit: User {UserId} performed {Action} on {EntityType} {EntityId} at {Timestamp}. Before: {Before}, After: {After}")]
    private static partial void LogAuditChange(
        ILogger logger,
        string userId,
        string action,
        string entityType,
        string entityId,
        DateTimeOffset timestamp,
        string before,
        string after);
}
