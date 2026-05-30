using OpenIdentityStack.Application.ApplicationPermissions.Validators;
using SharedKernel;

namespace OpenIdentityStack.Application.ApplicationPermissions;

public interface IRemotePermissionManifestFetcher
{
    Task<Result<PermissionManifestDocument>> FetchAsync(
        string manifestBaseUrl,
        string expectedApplicationIdentifier,
        CancellationToken cancellationToken = default);
}
