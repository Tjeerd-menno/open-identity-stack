using OpenIdentityStack.Application.Abstractions;

namespace OpenIdentityStack.Application.ApplicationPermissions;

public sealed class ApplicationPermissionAuditWriter : IApplicationPermissionAuditWriter
{
    private readonly IAuditLog auditLog;

    public ApplicationPermissionAuditWriter(IAuditLog auditLog)
    {
        this.auditLog = auditLog;
    }

    public async Task WriteAsync(
        string action,
        string actorId,
        string? applicationId,
        string? result,
        CancellationToken cancellationToken = default)
    {
        await this.auditLog.LogAsync(
            actorId,
            action,
            "RegisteredApplication",
            applicationId ?? "unknown",
            result,
            cancellationToken).ConfigureAwait(false);
    }
}
