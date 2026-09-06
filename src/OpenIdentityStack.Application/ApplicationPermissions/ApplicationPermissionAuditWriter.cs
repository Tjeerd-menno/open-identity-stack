using OpenIdentityStack.Application.Abstractions;

namespace OpenIdentityStack.Application.ApplicationPermissions;

public sealed class ApplicationPermissionAuditWriter : IApplicationPermissionAuditWriter
{
    private readonly IAuditLog auditLog;
    private readonly IAdministrativeActorContext actorContext;

    public ApplicationPermissionAuditWriter(IAuditLog auditLog, IAdministrativeActorContext actorContext)
    {
        this.auditLog = auditLog;
        this.actorContext = actorContext;
    }

    public async Task WriteAsync(
        string action,
        string actorId,
        string? applicationId,
        string? result,
        CancellationToken cancellationToken = default)
    {
        await this.auditLog.LogAsync(
            this.actorContext.AuditActorId is { } authenticatedActor && authenticatedActor != "system" ? authenticatedActor : actorId,
            action,
            "RegisteredApplication",
            applicationId ?? "unknown",
            result,
            cancellationToken).ConfigureAwait(false);
    }
}
