using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.ApplicationPermissions;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Application.ApplicationPermissions;

public sealed class ApplicationPermissionAuthorizationService : IApplicationPermissionAuthorizationService
{
    private readonly IPermissionChecker permissionChecker;
    private readonly IGroupRepository groupRepository;

    public ApplicationPermissionAuthorizationService(IPermissionChecker permissionChecker, IGroupRepository groupRepository)
    {
        this.permissionChecker = permissionChecker;
        this.groupRepository = groupRepository;
    }

    public async Task<bool> CanRegisterApplicationAsync(string actorId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(actorId, out _))
        {
            return false;
        }

        return await this.HasRegistryPermissionAsync(actorId, Permissions.ApplicationPermissions.Write, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> CanAdministerRegistryAsync(string actorId, CancellationToken cancellationToken = default)
    {
        return await this.HasRegistryPermissionAsync(actorId, Permissions.ApplicationPermissions.Admin, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> CanManageApplicationAsync(string actorId, string applicationOwnerId, CancellationToken cancellationToken = default)
    {
        if (string.Equals(actorId, applicationOwnerId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return await this.HasRegistryPermissionAsync(actorId, Permissions.ApplicationPermissions.Admin, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> CanManageApplicationAsync(string actorId, RegisteredApplication application, CancellationToken cancellationToken = default)
    {
        if (application.OwnerType == OwnerType.User
            && string.Equals(actorId, application.OwnerId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (application.Maintainers.Any(maintainer =>
            maintainer.PrincipalType == OwnerType.User
            && string.Equals(actorId, maintainer.PrincipalId, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        IReadOnlyList<Group> actorGroups = await this.GetActorGroupsAsync(actorId, cancellationToken).ConfigureAwait(false);
        if (actorGroups.Count > 0)
        {
            if (application.OwnerType == OwnerType.Group && IsActorInGroup(application.OwnerId, actorGroups))
            {
                return true;
            }

            if (application.Maintainers.Any(maintainer =>
                maintainer.PrincipalType == OwnerType.Group
                && IsActorInGroup(maintainer.PrincipalId, actorGroups)))
            {
                return true;
            }
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

    private async Task<IReadOnlyList<Group>> GetActorGroupsAsync(string actorId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(actorId, out Guid userId))
        {
            return [];
        }

        return await this.groupRepository.GetGroupsForUserAsync(new UserId(userId), cancellationToken).ConfigureAwait(false);
    }

    private static bool IsActorInGroup(string groupId, IReadOnlyList<Group> actorGroups)
    {
        return Guid.TryParse(groupId, out Guid parsedGroupId)
            && actorGroups.Any(group => group.Id.Value == parsedGroupId);
    }
}
