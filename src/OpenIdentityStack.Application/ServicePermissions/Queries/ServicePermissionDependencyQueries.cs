using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Application.ServicePermissions.Queries;

public sealed record GetPermissionDependenciesQuery(Guid PermissionId);

public interface IGetPermissionDependenciesQueryHandler
{
    Task<Result<IReadOnlyList<RoleAssignmentDependency>>> HandleAsync(GetPermissionDependenciesQuery query, CancellationToken cancellationToken = default);
}

public sealed class GetPermissionDependenciesQueryHandler : IGetPermissionDependenciesQueryHandler
{
    private readonly IServicePermissionRegistryRepository repository;
    private readonly IRolePermissionDependencyReader dependencyReader;

    public GetPermissionDependenciesQueryHandler(
        IServicePermissionRegistryRepository repository,
        IRolePermissionDependencyReader dependencyReader)
    {
        this.repository = repository;
        this.dependencyReader = dependencyReader;
    }

    public async Task<Result<IReadOnlyList<RoleAssignmentDependency>>> HandleAsync(
        GetPermissionDependenciesQuery query,
        CancellationToken cancellationToken = default)
    {
        RegisteredService? service = await this.repository
            .GetByPermissionIdAsync(new ServicePermissionId(query.PermissionId), cancellationToken)
            .ConfigureAwait(false);
        ServicePermission? permission = service?.Permissions.FirstOrDefault(p => p.Id.Value == query.PermissionId);
        if (permission is null)
        {
            return DomainError.NotFound("ServicePermission.NotFound", $"Permission '{query.PermissionId}' not found.");
        }

        IReadOnlyList<RoleAssignmentDependency> dependencies = await this.dependencyReader
            .GetDependenciesAsync(permission.FullPermissionKey, cancellationToken)
            .ConfigureAwait(false);
        Result<IReadOnlyList<RoleAssignmentDependency>> result = dependencies.ToList();
        return result;
    }
}
