using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.ApplicationPermissions.Queries;

public sealed record GetPermissionDependenciesQuery(Guid PermissionId);

public interface IGetPermissionDependenciesQueryHandler
{
    Task<Result<IReadOnlyList<RoleAssignmentDependency>>> HandleAsync(GetPermissionDependenciesQuery query, CancellationToken cancellationToken = default);
}

public sealed class GetPermissionDependenciesQueryHandler : IGetPermissionDependenciesQueryHandler
{
    private readonly IApplicationPermissionRegistryRepository repository;
    private readonly IRolePermissionDependencyReader dependencyReader;

    public GetPermissionDependenciesQueryHandler(
        IApplicationPermissionRegistryRepository repository,
        IRolePermissionDependencyReader dependencyReader)
    {
        this.repository = repository;
        this.dependencyReader = dependencyReader;
    }

    public async Task<Result<IReadOnlyList<RoleAssignmentDependency>>> HandleAsync(
        GetPermissionDependenciesQuery query,
        CancellationToken cancellationToken = default)
    {
        RegisteredApplication? application = await this.repository
            .GetByPermissionIdAsync(new ApplicationPermissionId(query.PermissionId), cancellationToken)
            .ConfigureAwait(false);
        ApplicationPermission? permission = application?.Permissions.FirstOrDefault(p => p.Id.Value == query.PermissionId);
        if (permission is null)
        {
            return DomainError.NotFound("ApplicationPermission.NotFound", $"Permission '{query.PermissionId}' not found.");
        }

        IReadOnlyList<RoleAssignmentDependency> dependencies = await this.dependencyReader
            .GetDependenciesAsync(permission.FullPermissionKey, cancellationToken)
            .ConfigureAwait(false);
        Result<IReadOnlyList<RoleAssignmentDependency>> result = dependencies.ToList();
        return result;
    }
}
