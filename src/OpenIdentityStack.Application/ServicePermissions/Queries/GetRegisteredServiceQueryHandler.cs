using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ServicePermissions.Dtos;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Application.ServicePermissions.Queries;

public interface IGetRegisteredServiceQueryHandler
{
    Task<Result<RegisteredServiceDto>> HandleAsync(GetRegisteredServiceQuery query, CancellationToken cancellationToken = default);
}

public sealed class GetRegisteredServiceQueryHandler : IGetRegisteredServiceQueryHandler
{
    private readonly IServicePermissionRegistryRepository repository;

    public GetRegisteredServiceQueryHandler(IServicePermissionRegistryRepository repository)
    {
        this.repository = repository;
    }

    public async Task<Result<RegisteredServiceDto>> HandleAsync(GetRegisteredServiceQuery query, CancellationToken cancellationToken = default)
    {
        RegisteredService? service = await this.repository.GetByIdAsync(new RegisteredServiceId(query.ServiceId), cancellationToken).ConfigureAwait(false);
        if (service is null)
        {
            return DomainError.NotFound("RegisteredService.NotFound", $"Service '{query.ServiceId}' not found.");
        }

        return MapToDto(service);
    }

    private static RegisteredServiceDto MapToDto(RegisteredService service)
    {
        var permissions = service.Permissions.Select(p => new ServicePermissionDto(
            p.Id.Value,
            p.PermissionKey,
            p.FullPermissionKey,
            p.DisplayName,
            p.Description,
            p.IntendedUse,
            p.DocumentationUrl,
            p.Status.ToString(),
            p.IsAssignable,
            p.CreatedAt,
            p.ModifiedAt,
            p.DeprecatedAt,
            p.DisabledAt,
            p.RetiredAt)).ToList();

        var maintainers = service.Maintainers.Select(m => new DelegatedMaintainerDto(
            m.Id.Value,
            m.PrincipalId,
            m.PrincipalType.ToString(),
            m.GrantedBy,
            m.GrantedAt)).ToList();

        return new RegisteredServiceDto(
            service.Id.Value,
            service.ServiceIdentifier,
            service.DisplayName,
            service.Description,
            service.OwnerId,
            service.OwnerType.ToString(),
            service.Status.ToString(),
            service.CreatedAt,
            service.ModifiedAt,
            service.ConcurrencyToken,
            permissions,
            maintainers);
    }
}
