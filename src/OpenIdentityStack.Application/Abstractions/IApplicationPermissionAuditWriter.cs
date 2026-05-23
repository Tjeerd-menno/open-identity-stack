namespace OpenIdentityStack.Application.Abstractions;

public interface IApplicationPermissionAuditWriter
{
    Task WriteAsync(string action, string actorId, string? applicationId, string? result, CancellationToken cancellationToken = default);
}
