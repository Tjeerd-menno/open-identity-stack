using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ServicePermissions.Dtos;
using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Application.ServicePermissions.Commands;

public interface IUpdateRegisteredServiceUseCase
{
    Task<Result<RegisteredServiceDto>> ExecuteAsync(UpdateRegisteredServiceCommand command, CancellationToken cancellationToken = default);
}

public interface IAddServicePermissionUseCase
{
    Task<Result<RegisteredServiceDto>> ExecuteAsync(AddServicePermissionCommand command, CancellationToken cancellationToken = default);
}

public interface IUpdateServicePermissionUseCase
{
    Task<Result<RegisteredServiceDto>> ExecuteAsync(UpdateServicePermissionCommand command, CancellationToken cancellationToken = default);
}

public interface IChangeRegisteredServiceLifecycleUseCase
{
    Task<Result<RegisteredServiceDto>> ExecuteAsync(ChangeRegisteredServiceLifecycleCommand command, CancellationToken cancellationToken = default);
}

public interface IChangeServicePermissionLifecycleUseCase
{
    Task<Result<RegisteredServiceDto>> ExecuteAsync(ChangeServicePermissionLifecycleCommand command, CancellationToken cancellationToken = default);
}

public interface ITransferRegisteredServiceOwnershipUseCase
{
    Task<Result<RegisteredServiceDto>> ExecuteAsync(TransferRegisteredServiceOwnershipCommand command, CancellationToken cancellationToken = default);
}

public interface IAddDelegatedMaintainerUseCase
{
    Task<Result<RegisteredServiceDto>> ExecuteAsync(AddDelegatedMaintainerCommand command, CancellationToken cancellationToken = default);
}

public interface IRemoveDelegatedMaintainerUseCase
{
    Task<Result<RegisteredServiceDto>> ExecuteAsync(RemoveDelegatedMaintainerCommand command, CancellationToken cancellationToken = default);
}

public sealed class ServicePermissionMaintenanceUseCases :
    IUpdateRegisteredServiceUseCase,
    IAddServicePermissionUseCase,
    IUpdateServicePermissionUseCase,
    IChangeRegisteredServiceLifecycleUseCase,
    IChangeServicePermissionLifecycleUseCase,
    ITransferRegisteredServiceOwnershipUseCase,
    IAddDelegatedMaintainerUseCase,
    IRemoveDelegatedMaintainerUseCase
{
    private readonly IServicePermissionRegistryRepository repository;
    private readonly IServicePermissionAuthorizationService authorizationService;
    private readonly IRolePermissionDependencyReader dependencyReader;
    private readonly IServicePermissionAuditWriter auditWriter;
    private readonly IDateTimeProvider dateTimeProvider;

    public ServicePermissionMaintenanceUseCases(
        IServicePermissionRegistryRepository repository,
        IServicePermissionAuthorizationService authorizationService,
        IRolePermissionDependencyReader dependencyReader,
        IServicePermissionAuditWriter auditWriter,
        IDateTimeProvider dateTimeProvider)
    {
        this.repository = repository;
        this.authorizationService = authorizationService;
        this.dependencyReader = dependencyReader;
        this.auditWriter = auditWriter;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<RegisteredServiceDto>> ExecuteAsync(UpdateRegisteredServiceCommand command, CancellationToken cancellationToken = default)
    {
        return await this.UpdateServiceAsync(
            command.ServiceId,
            command.ActorId,
            command.ExpectedConcurrencyToken,
            "UpdateService",
            service => service.UpdateMetadata(command.DisplayName, command.Description, command.ActorId, this.dateTimeProvider),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredServiceDto>> ExecuteAsync(AddServicePermissionCommand command, CancellationToken cancellationToken = default)
    {
        return await this.UpdateServiceAsync(
            command.ServiceId,
            command.ActorId,
            command.ExpectedConcurrencyToken,
            "AddPermission",
            service =>
            {
                Result<ServicePermission> result = service.AddPermission(
                    command.PermissionKey,
                    command.DisplayName,
                    command.Description,
                    command.IntendedUse,
                    command.DocumentationUrl,
                    command.ActorId,
                    this.dateTimeProvider);
                return result.IsSuccess ? Result.Success() : result.Error;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredServiceDto>> ExecuteAsync(UpdateServicePermissionCommand command, CancellationToken cancellationToken = default)
    {
        RegisteredService? service = await this.repository
            .GetByPermissionIdAsync(new ServicePermissionId(command.PermissionId), cancellationToken)
            .ConfigureAwait(false);
        if (service is null)
        {
            return DomainError.NotFound("ServicePermission.NotFound", $"Permission '{command.PermissionId}' not found.");
        }

        return await this.UpdateLoadedServiceAsync(
            service,
            command.ActorId,
            command.ExpectedConcurrencyToken,
            "UpdatePermission",
            current => current.UpdatePermission(
                new ServicePermissionId(command.PermissionId),
                command.DisplayName,
                command.Description,
                command.IntendedUse,
                command.DocumentationUrl,
                command.ActorId,
                this.dateTimeProvider),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredServiceDto>> ExecuteAsync(ChangeRegisteredServiceLifecycleCommand command, CancellationToken cancellationToken = default)
    {
        RegisteredService? service = await this.repository.GetByIdAsync(new RegisteredServiceId(command.ServiceId), cancellationToken).ConfigureAwait(false);
        if (service is null)
        {
            return DomainError.NotFound("RegisteredService.NotFound", $"Service '{command.ServiceId}' not found.");
        }

        bool hasBlockingDependencies = false;
        foreach (ServicePermission permission in service.Permissions)
        {
            if (await this.HasBlockingDependenciesAsync(permission.FullPermissionKey, cancellationToken).ConfigureAwait(false))
            {
                hasBlockingDependencies = true;
                break;
            }
        }

        return await this.UpdateServiceAsync(
            command.ServiceId,
            command.ActorId,
            command.ExpectedConcurrencyToken,
            "ChangeServiceLifecycle",
            current => current.ChangeStatus(command.Status, hasBlockingDependencies && !command.AcknowledgeDependencies, command.ActorId, this.dateTimeProvider),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredServiceDto>> ExecuteAsync(ChangeServicePermissionLifecycleCommand command, CancellationToken cancellationToken = default)
    {
        RegisteredService? service = await this.repository
            .GetByPermissionIdAsync(new ServicePermissionId(command.PermissionId), cancellationToken)
            .ConfigureAwait(false);
        if (service is null)
        {
            return DomainError.NotFound("ServicePermission.NotFound", $"Permission '{command.PermissionId}' not found.");
        }

        ServicePermission? permission = service.Permissions.FirstOrDefault(p => p.Id.Value == command.PermissionId);
        if (permission is null)
        {
            return DomainError.NotFound("ServicePermission.NotFound", $"Permission '{command.PermissionId}' not found.");
        }

        bool hasBlockingDependencies = await this.HasBlockingDependenciesAsync(permission.FullPermissionKey, cancellationToken).ConfigureAwait(false);
        return await this.UpdateLoadedServiceAsync(
            service,
            command.ActorId,
            command.ExpectedConcurrencyToken,
            "ChangePermissionLifecycle",
            current => current.ChangePermissionStatus(
                new ServicePermissionId(command.PermissionId),
                command.Status,
                hasBlockingDependencies && !command.AcknowledgeDependencies,
                command.ActorId,
                this.dateTimeProvider),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredServiceDto>> ExecuteAsync(TransferRegisteredServiceOwnershipCommand command, CancellationToken cancellationToken = default)
    {
        return await this.UpdateServiceAsync(
            command.ServiceId,
            command.ActorId,
            command.ExpectedConcurrencyToken,
            "TransferOwnership",
            service => service.TransferOwnership(command.OwnerId, command.OwnerType, command.ActorId, this.dateTimeProvider),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredServiceDto>> ExecuteAsync(AddDelegatedMaintainerCommand command, CancellationToken cancellationToken = default)
    {
        return await this.UpdateServiceAsync(
            command.ServiceId,
            command.ActorId,
            command.ExpectedConcurrencyToken,
            "AddMaintainer",
            service => service.AddMaintainer(command.PrincipalId, command.PrincipalType, command.ActorId, this.dateTimeProvider),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredServiceDto>> ExecuteAsync(RemoveDelegatedMaintainerCommand command, CancellationToken cancellationToken = default)
    {
        return await this.UpdateServiceAsync(
            command.ServiceId,
            command.ActorId,
            command.ExpectedConcurrencyToken,
            "RemoveMaintainer",
            service => service.RemoveMaintainer(command.PrincipalId, command.ActorId, this.dateTimeProvider),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<RegisteredServiceDto>> UpdateServiceAsync(
        Guid serviceId,
        string actorId,
        uint? expectedConcurrencyToken,
        string action,
        Func<RegisteredService, Result> mutate,
        CancellationToken cancellationToken)
    {
        RegisteredService? service = await this.repository.GetByIdAsync(new RegisteredServiceId(serviceId), cancellationToken).ConfigureAwait(false);
        if (service is null)
        {
            return DomainError.NotFound("RegisteredService.NotFound", $"Service '{serviceId}' not found.");
        }

        return await this.UpdateLoadedServiceAsync(service, actorId, expectedConcurrencyToken, action, mutate, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<RegisteredServiceDto>> UpdateLoadedServiceAsync(
        RegisteredService service,
        string actorId,
        uint? expectedConcurrencyToken,
        string action,
        Func<RegisteredService, Result> mutate,
        CancellationToken cancellationToken)
    {
        if (!await this.authorizationService.CanManageServiceAsync(actorId, service.OwnerId, cancellationToken).ConfigureAwait(false)
            && !service.CanBeManagedBy(actorId))
        {
            await this.auditWriter.WriteAsync(action, actorId, service.Id.Value.ToString(), "Denied", cancellationToken).ConfigureAwait(false);
            return DomainError.Forbidden("ServicePermission.Forbidden", "Actor cannot manage this registered service.");
        }

        if (expectedConcurrencyToken.HasValue && service.ConcurrencyToken != expectedConcurrencyToken.Value)
        {
            return DomainError.Conflict("ServicePermission.ConcurrencyConflict", "The registered service was modified by another request.");
        }

        Result mutationResult = mutate(service);
        if (mutationResult.IsFailure)
        {
            await this.auditWriter.WriteAsync(action, actorId, service.Id.Value.ToString(), mutationResult.Error.Code, cancellationToken).ConfigureAwait(false);
            return mutationResult.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await this.auditWriter.WriteAsync(action, actorId, service.Id.Value.ToString(), "Succeeded", cancellationToken).ConfigureAwait(false);
        return MapToDto(service);
    }

    private async Task<bool> HasBlockingDependenciesAsync(string fullPermissionKey, CancellationToken cancellationToken)
    {
        IReadOnlyList<RoleAssignmentDependency> dependencies = await this.dependencyReader
            .GetDependenciesAsync(fullPermissionKey, cancellationToken)
            .ConfigureAwait(false);
        return dependencies.Any(d => d is { IsActive: true, Impact: DependencyImpact.BlocksRetirement or DependencyImpact.BlocksDeletion });
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
