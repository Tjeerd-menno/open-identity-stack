namespace OpenIdentityStack.Application.Abstractions;

public interface IApplicationPermissionAuthorizationService
{
    Task<bool> CanRegisterApplicationAsync(string actorId, CancellationToken cancellationToken = default);

    Task<bool> CanManageApplicationAsync(string actorId, string applicationOwnerId, CancellationToken cancellationToken = default);
}
