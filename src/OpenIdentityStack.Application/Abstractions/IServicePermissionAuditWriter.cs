namespace OpenIdentityStack.Application.Abstractions;

public interface IServicePermissionAuditWriter
{
    Task WriteAsync(string action, string actorId, string? serviceId, string? result, CancellationToken cancellationToken = default);
}
