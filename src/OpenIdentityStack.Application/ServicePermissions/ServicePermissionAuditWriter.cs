using OpenIdentityStack.Application.Abstractions;

namespace OpenIdentityStack.Application.ServicePermissions;

public sealed class ServicePermissionAuditWriter : IServicePermissionAuditWriter
{
    private readonly IAuditLog auditLog;

    public ServicePermissionAuditWriter(IAuditLog auditLog)
    {
        this.auditLog = auditLog;
    }

    public async Task WriteAsync(
        string action,
        string actorId,
        string? serviceId,
        string? result,
        CancellationToken cancellationToken = default)
    {
        await this.auditLog.LogAsync(
            actorId,
            action,
            "RegisteredService",
            serviceId ?? "unknown",
            result,
            cancellationToken).ConfigureAwait(false);
    }
}
