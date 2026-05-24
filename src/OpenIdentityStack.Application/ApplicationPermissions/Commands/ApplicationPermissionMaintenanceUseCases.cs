using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.ApplicationPermissions.Commands;

public interface IUpdateRegisteredApplicationUseCase
{
    Task<Result<RegisteredApplicationDto>> ExecuteAsync(UpdateRegisteredApplicationCommand command, CancellationToken cancellationToken = default);
}

public interface IAddApplicationPermissionUseCase
{
    Task<Result<RegisteredApplicationDto>> ExecuteAsync(AddApplicationPermissionCommand command, CancellationToken cancellationToken = default);
}

public interface IUpdateApplicationPermissionUseCase
{
    Task<Result<RegisteredApplicationDto>> ExecuteAsync(UpdateApplicationPermissionCommand command, CancellationToken cancellationToken = default);
}

public interface IChangeRegisteredApplicationLifecycleUseCase
{
    Task<Result<RegisteredApplicationDto>> ExecuteAsync(ChangeRegisteredApplicationLifecycleCommand command, CancellationToken cancellationToken = default);
}

public interface ITransferRegisteredApplicationOwnershipUseCase
{
    Task<Result<RegisteredApplicationDto>> ExecuteAsync(TransferRegisteredApplicationOwnershipCommand command, CancellationToken cancellationToken = default);
}

public interface IAddDelegatedMaintainerUseCase
{
    Task<Result<RegisteredApplicationDto>> ExecuteAsync(AddDelegatedMaintainerCommand command, CancellationToken cancellationToken = default);
}

public interface IRemoveDelegatedMaintainerUseCase
{
    Task<Result<RegisteredApplicationDto>> ExecuteAsync(RemoveDelegatedMaintainerCommand command, CancellationToken cancellationToken = default);
}

public sealed class ApplicationPermissionMaintenanceUseCases :
    IUpdateRegisteredApplicationUseCase,
    IAddApplicationPermissionUseCase,
    IUpdateApplicationPermissionUseCase,
    IChangeRegisteredApplicationLifecycleUseCase,
    ITransferRegisteredApplicationOwnershipUseCase,
    IAddDelegatedMaintainerUseCase,
    IRemoveDelegatedMaintainerUseCase
{
    private readonly IApplicationPermissionRegistryRepository repository;
    private readonly IApplicationPermissionAuthorizationService authorizationService;
    private readonly IRolePermissionDependencyReader dependencyReader;
    private readonly IApplicationPermissionAuditWriter auditWriter;
    private readonly IDateTimeProvider dateTimeProvider;

    public ApplicationPermissionMaintenanceUseCases(
        IApplicationPermissionRegistryRepository repository,
        IApplicationPermissionAuthorizationService authorizationService,
        IRolePermissionDependencyReader dependencyReader,
        IApplicationPermissionAuditWriter auditWriter,
        IDateTimeProvider dateTimeProvider)
    {
        this.repository = repository;
        this.authorizationService = authorizationService;
        this.dependencyReader = dependencyReader;
        this.auditWriter = auditWriter;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<RegisteredApplicationDto>> ExecuteAsync(UpdateRegisteredApplicationCommand command, CancellationToken cancellationToken = default)
    {
        return await this.UpdateApplicationAsync(
            command.ApplicationId,
            command.ActorId,
            command.ExpectedConcurrencyToken,
            "UpdateApplication",
            application => application.UpdateMetadata(command.DisplayName, command.Description, command.ActorId, this.dateTimeProvider),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredApplicationDto>> ExecuteAsync(AddApplicationPermissionCommand command, CancellationToken cancellationToken = default)
    {
        return await this.UpdateApplicationAsync(
            command.ApplicationId,
            command.ActorId,
            command.ExpectedConcurrencyToken,
            "AddPermission",
            application =>
            {
                Result<ApplicationPermission> result = application.AddPermission(
                    command.PermissionKey,
                    command.DisplayName,
                    command.Description,
                    command.Category,
                    command.ActorId,
                    this.dateTimeProvider);
                return result.IsSuccess ? Result.Success() : result.Error;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredApplicationDto>> ExecuteAsync(UpdateApplicationPermissionCommand command, CancellationToken cancellationToken = default)
    {
        RegisteredApplication? application = await this.repository
            .GetByPermissionIdAsync(new ApplicationPermissionId(command.PermissionId), cancellationToken)
            .ConfigureAwait(false);
        if (application is null)
        {
            return DomainError.NotFound("ApplicationPermission.NotFound", $"Permission '{command.PermissionId}' not found.");
        }

        return await this.UpdateLoadedApplicationAsync(
            application,
            command.ActorId,
            command.ExpectedConcurrencyToken,
            "UpdatePermission",
            current => current.UpdatePermission(
                new ApplicationPermissionId(command.PermissionId),
                command.DisplayName,
                command.Description,
                command.Category,
                command.ActorId,
                this.dateTimeProvider),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredApplicationDto>> ExecuteAsync(ChangeRegisteredApplicationLifecycleCommand command, CancellationToken cancellationToken = default)
    {
        RegisteredApplication? application = await this.repository.GetByIdAsync(new RegisteredApplicationId(command.ApplicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return DomainError.NotFound("RegisteredApplication.NotFound", $"Application '{command.ApplicationId}' not found.");
        }

        bool hasBlockingDependencies = false;
        foreach (ApplicationPermission permission in application.Permissions)
        {
            if (await this.HasBlockingDependenciesAsync(permission.FullPermissionKey, cancellationToken).ConfigureAwait(false))
            {
                hasBlockingDependencies = true;
                break;
            }
        }

        return await this.UpdateApplicationAsync(
            command.ApplicationId,
            command.ActorId,
            command.ExpectedConcurrencyToken,
            "ChangeApplicationLifecycle",
            current => current.ChangeStatus(command.Status, hasBlockingDependencies && !command.AcknowledgeDependencies, command.ActorId, this.dateTimeProvider),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredApplicationDto>> ExecuteAsync(TransferRegisteredApplicationOwnershipCommand command, CancellationToken cancellationToken = default)
    {
        return await this.UpdateApplicationAsync(
            command.ApplicationId,
            command.ActorId,
            command.ExpectedConcurrencyToken,
            "TransferOwnership",
            application => application.TransferOwnership(command.OwnerId, command.OwnerType, command.ActorId, this.dateTimeProvider),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredApplicationDto>> ExecuteAsync(AddDelegatedMaintainerCommand command, CancellationToken cancellationToken = default)
    {
        return await this.UpdateApplicationAsync(
            command.ApplicationId,
            command.ActorId,
            command.ExpectedConcurrencyToken,
            "AddMaintainer",
            application => application.AddMaintainer(command.PrincipalId, command.PrincipalType, command.ActorId, this.dateTimeProvider),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredApplicationDto>> ExecuteAsync(RemoveDelegatedMaintainerCommand command, CancellationToken cancellationToken = default)
    {
        return await this.UpdateApplicationAsync(
            command.ApplicationId,
            command.ActorId,
            command.ExpectedConcurrencyToken,
            "RemoveMaintainer",
            application => application.RemoveMaintainer(command.PrincipalId, command.ActorId, this.dateTimeProvider),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<RegisteredApplicationDto>> UpdateApplicationAsync(
        Guid applicationId,
        string actorId,
        uint? expectedConcurrencyToken,
        string action,
        Func<RegisteredApplication, Result> mutate,
        CancellationToken cancellationToken)
    {
        RegisteredApplication? application = await this.repository.GetByIdAsync(new RegisteredApplicationId(applicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return DomainError.NotFound("RegisteredApplication.NotFound", $"Application '{applicationId}' not found.");
        }

        return await this.UpdateLoadedApplicationAsync(application, actorId, expectedConcurrencyToken, action, mutate, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<RegisteredApplicationDto>> UpdateLoadedApplicationAsync(
        RegisteredApplication application,
        string actorId,
        uint? expectedConcurrencyToken,
        string action,
        Func<RegisteredApplication, Result> mutate,
        CancellationToken cancellationToken)
    {
        if (!await this.authorizationService.CanManageApplicationAsync(actorId, application.OwnerId, cancellationToken).ConfigureAwait(false)
            && !application.CanBeManagedBy(actorId))
        {
            await this.auditWriter.WriteAsync(action, actorId, application.Id.Value.ToString(), "Denied", cancellationToken).ConfigureAwait(false);
            return DomainError.Forbidden("ApplicationPermission.Forbidden", "Actor cannot manage this registered application.");
        }

        if (expectedConcurrencyToken.HasValue && application.ConcurrencyToken != expectedConcurrencyToken.Value)
        {
            return DomainError.Conflict("ApplicationPermission.ConcurrencyConflict", "The registered application was modified by another request.");
        }

        Result mutationResult = mutate(application);
        if (mutationResult.IsFailure)
        {
            await this.auditWriter.WriteAsync(action, actorId, application.Id.Value.ToString(), mutationResult.Error.Code, cancellationToken).ConfigureAwait(false);
            return mutationResult.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await this.auditWriter.WriteAsync(action, actorId, application.Id.Value.ToString(), "Succeeded", cancellationToken).ConfigureAwait(false);
        return MapToDto(application);
    }

    private async Task<bool> HasBlockingDependenciesAsync(string fullPermissionKey, CancellationToken cancellationToken)
    {
        IReadOnlyList<RoleAssignmentDependency> dependencies = await this.dependencyReader
            .GetDependenciesAsync(fullPermissionKey, cancellationToken)
            .ConfigureAwait(false);
        return dependencies.Any(d => d is { IsActive: true, Impact: DependencyImpact.BlocksRetirement or DependencyImpact.BlocksDeletion });
    }

    private static RegisteredApplicationDto MapToDto(RegisteredApplication application)
    {
        var permissions = application.Permissions.Select(p => new ApplicationPermissionDto(
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
            application.Description)).ToList();

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
            maintainers);
    }
}
