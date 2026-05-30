using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.ApplicationPermissions.Queries;

public interface IGetRegisteredApplicationQueryHandler
{
    Task<Result<RegisteredApplicationDto>> HandleAsync(GetRegisteredApplicationQuery query, CancellationToken cancellationToken = default);
}

public sealed class GetRegisteredApplicationQueryHandler : IGetRegisteredApplicationQueryHandler
{
    private readonly IApplicationPermissionRegistryRepository repository;

    public GetRegisteredApplicationQueryHandler(IApplicationPermissionRegistryRepository repository)
    {
        this.repository = repository;
    }

    public async Task<Result<RegisteredApplicationDto>> HandleAsync(GetRegisteredApplicationQuery query, CancellationToken cancellationToken = default)
    {
        RegisteredApplication? application = await this.repository.GetByIdAsync(new RegisteredApplicationId(query.ApplicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return DomainError.NotFound("RegisteredApplication.NotFound", $"Application '{query.ApplicationId}' not found.");
        }

        return MapToDto(application);
    }

    private static RegisteredApplicationDto MapToDto(RegisteredApplication application)
    {
        var permissions = application.Permissions.Where(static p => !p.IsRemoved).Select(p => new ApplicationPermissionDto(
            p.Id.Value,
            p.PermissionKey,
            p.FullPermissionKey,
            p.DisplayName,
            p.Description,
            p.Category,
            p.CreatedAt,
            p.ModifiedAt,
            application.ApplicationIdentifier,
            application.DisplayName,
            application.ManifestVersion)).ToList();

        var maintainers = application.Maintainers.Select(m => new DelegatedMaintainerDto(
            m.Id.Value,
            m.PrincipalId,
            m.PrincipalType.ToString(),
            m.GrantedBy,
            m.GrantedAt)).ToList();

        return new RegisteredApplicationDto(
            application.Id.Value,
            application.ApplicationIdentifier,
            application.DisplayName,
            application.Description,
            application.OwnerId,
            application.OwnerType.ToString(),
            application.Status.ToString(),
            application.CreatedAt,
            application.ModifiedAt,
            application.ConcurrencyToken,
            permissions,
            maintainers,
            application.SchemaVersion,
            application.ManifestVersion,
            application.ManifestBaseUrl);
    }
}
