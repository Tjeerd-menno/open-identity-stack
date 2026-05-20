using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Application.ServicePermissions;

public sealed class ServicePermissionAuthorizationService : IServicePermissionAuthorizationService
{
    private readonly IPermissionChecker permissionChecker;

    public ServicePermissionAuthorizationService(IPermissionChecker permissionChecker)
    {
        this.permissionChecker = permissionChecker;
    }

    public async Task<bool> CanRegisterServiceAsync(string actorId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(actorId, out _))
        {
            return !string.IsNullOrWhiteSpace(actorId);
        }

        return await this.HasRegistryPermissionAsync(actorId, Permissions.ServicePermissions.Write, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> CanManageServiceAsync(string actorId, string serviceOwnerId, CancellationToken cancellationToken = default)
    {
        if (string.Equals(actorId, serviceOwnerId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return await this.HasRegistryPermissionAsync(actorId, Permissions.ServicePermissions.Admin, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> HasRegistryPermissionAsync(string actorId, string permission, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(actorId, out Guid userId))
        {
            return false;
        }

        return await this.permissionChecker.HasAnyPermissionAsync(
            new UserId(userId),
            [permission, Permissions.ServicePermissions.All, Permissions.All],
            cancellationToken).ConfigureAwait(false);
    }
}
