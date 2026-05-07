namespace OpenIdentityStack.Application.Abstractions;

public interface IServicePermissionAuthorizationService
{
    Task<bool> CanRegisterServiceAsync(string actorId, CancellationToken cancellationToken = default);

    Task<bool> CanManageServiceAsync(string actorId, string serviceOwnerId, CancellationToken cancellationToken = default);
}
