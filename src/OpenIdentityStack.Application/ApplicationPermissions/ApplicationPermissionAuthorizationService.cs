using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Application.ApplicationPermissions;

public sealed class ApplicationPermissionAuthorizationService : IApplicationPermissionAuthorizationService
{
    private readonly IPermissionChecker permissionChecker;

    public ApplicationPermissionAuthorizationService(IPermissionChecker permissionChecker)
    {
        this.permissionChecker = permissionChecker;
    }

    public async Task<bool> CanRegisterApplicationAsync(string actorId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(actorId, out _))
        {
            return false;
        }

        return await this.HasRegistryPermissionAsync(actorId, Permissions.ApplicationPermissions.Write, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> CanManageApplicationAsync(string actorId, string applicationOwnerId, CancellationToken cancellationToken = default)
    {
        if (string.Equals(actorId, applicationOwnerId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return await this.HasRegistryPermissionAsync(actorId, Permissions.ApplicationPermissions.Admin, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> HasRegistryPermissionAsync(string actorId, string permission, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(actorId, out Guid userId))
        {
            return false;
        }

        return await this.permissionChecker.HasAnyPermissionAsync(
            new UserId(userId),
            [permission, Permissions.ApplicationPermissions.All, Permissions.All],
            cancellationToken).ConfigureAwait(false);
    }
}
