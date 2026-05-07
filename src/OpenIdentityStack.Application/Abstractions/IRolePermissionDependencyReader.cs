using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Application.Abstractions;

public interface IRolePermissionDependencyReader
{
    Task<IReadOnlyList<RoleAssignmentDependency>> GetDependenciesAsync(string fullPermissionKey, CancellationToken cancellationToken = default);
}
