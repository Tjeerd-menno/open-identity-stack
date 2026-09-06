using Microsoft.Extensions.DependencyInjection;
using OpenIdentityStack.Application.Abstractions;

namespace OpenIdentityStack.Infrastructure.Audit;

/// <summary>Approval logging must never save pending role or group mutations as a side effect.</summary>
public sealed class AdministrativeApprovalAudit(IServiceScopeFactory scopeFactory) : IAdministrativeApprovalAudit
{
    public async Task LogAsync(string userId, string action, string entityType, string entityId,
        string? details = null, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAuditLog>()
            .LogAsync(userId, action, entityType, entityId, details, cancellationToken);
    }

    public async Task LogChangeAsync(string userId, string action, string entityType, string entityId,
        string? beforeState, string? afterState, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAuditLog>()
            .LogChangeAsync(userId, action, entityType, entityId, beforeState, afterState, cancellationToken);
    }
}
