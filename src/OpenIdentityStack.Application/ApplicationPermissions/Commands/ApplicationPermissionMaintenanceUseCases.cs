using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Application.ApplicationPermissions.Validators;
using OpenIdentityStack.Application.Authorization;
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

public interface IDeleteApplicationPermissionUseCase
{
    Task<Result<DestructiveOperationResultDto>> ExecuteAsync(DeleteApplicationPermissionCommand command, CancellationToken cancellationToken = default);
}

public interface IPreviewApplicationPermissionDeletionImpactUseCase
{
    Task<Result<DeletionImpactDto>> ExecuteAsync(PreviewDeleteApplicationPermissionCommand command, CancellationToken cancellationToken = default);
}

public interface IDeleteRegisteredApplicationUseCase
{
    Task<Result<DestructiveOperationResultDto>> ExecuteAsync(DeleteRegisteredApplicationCommand command, CancellationToken cancellationToken = default);
}

public interface IPreviewRegisteredApplicationDeletionImpactUseCase
{
    Task<Result<DeletionImpactDto>> ExecuteAsync(PreviewDeleteRegisteredApplicationCommand command, CancellationToken cancellationToken = default);
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

public interface IUpdateRemovedPermissionReplacementUseCase
{
    Task<Result<RemovedPermissionDetailDto>> ExecuteAsync(UpdateRemovedPermissionReplacementCommand command, CancellationToken cancellationToken = default);
}

public interface IListApplicationPermissionHistoryQueryHandler
{
    Task<Result<ApplicationPermissionHistoryDto>> HandleAsync(ListApplicationPermissionHistoryQuery query, CancellationToken cancellationToken = default);
}

public interface IListApplicationPermissionDiagnosticsQueryHandler
{
    Task<Result<PermissionDiagnosticsDto>> HandleAsync(ListApplicationPermissionDiagnosticsQuery query, CancellationToken cancellationToken = default);
}

public sealed class ApplicationPermissionMaintenanceUseCases :
    IUpdateRegisteredApplicationUseCase,
    IAddApplicationPermissionUseCase,
    IUpdateApplicationPermissionUseCase,
    IDeleteApplicationPermissionUseCase,
    IPreviewApplicationPermissionDeletionImpactUseCase,
    IDeleteRegisteredApplicationUseCase,
    IPreviewRegisteredApplicationDeletionImpactUseCase,
    IChangeRegisteredApplicationLifecycleUseCase,
    ITransferRegisteredApplicationOwnershipUseCase,
    IAddDelegatedMaintainerUseCase,
    IRemoveDelegatedMaintainerUseCase,
    IUpdateRemovedPermissionReplacementUseCase,
    IListApplicationPermissionHistoryQueryHandler,
    IListApplicationPermissionDiagnosticsQueryHandler
{
    public static readonly DomainError ManifestBackedApplicationReadOnly = DomainError.Conflict(
        "RegisteredApplication.ManifestBackedApplicationReadOnly",
        "Imported applications are managed by their permission manifest and cannot be edited manually.");

    public static readonly DomainError ManualApplicationCannotBeManifestBacked = DomainError.Validation(
        "RegisteredApplication.ManualApplicationCannotBeManifestBacked",
        "Manually registered applications cannot be backed by a permission manifest.");

    private readonly IApplicationPermissionRegistryRepository repository;
    private readonly IApplicationPermissionAuthorizationService authorizationService;
    private readonly IRolePermissionDependencyReader dependencyReader;
    private readonly IPermissionAssignmentStore permissionAssignmentStore;
    private readonly IApplicationPermissionAuditWriter auditWriter;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IApplicationPermissionTransactionRunner transactionRunner;
    private readonly IPermissionDiagnosticsReader? diagnosticsReader;

    public ApplicationPermissionMaintenanceUseCases(
        IApplicationPermissionRegistryRepository repository,
        IApplicationPermissionAuthorizationService authorizationService,
        IRolePermissionDependencyReader dependencyReader,
        IApplicationPermissionAuditWriter auditWriter,
        IDateTimeProvider dateTimeProvider,
        IPermissionAssignmentStore permissionAssignmentStore,
        IApplicationPermissionTransactionRunner transactionRunner,
        IPermissionDiagnosticsReader? diagnosticsReader = null)
    {
        this.repository = repository;
        this.authorizationService = authorizationService;
        this.dependencyReader = dependencyReader;
        this.permissionAssignmentStore = permissionAssignmentStore;
        this.auditWriter = auditWriter;
        this.dateTimeProvider = dateTimeProvider;
        this.transactionRunner = transactionRunner;
        this.diagnosticsReader = diagnosticsReader;
    }

    public async Task<Result<RegisteredApplicationDto>> ExecuteAsync(UpdateRegisteredApplicationCommand command, CancellationToken cancellationToken = default)
    {
        RegisteredApplication? application = await this.repository.GetByIdAsync(new RegisteredApplicationId(command.ApplicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return DomainError.NotFound("RegisteredApplication.NotFound", $"Application '{command.ApplicationId}' not found.");
        }

        if (!await this.authorizationService.CanManageApplicationAsync(command.ActorId, application, cancellationToken).ConfigureAwait(false))
        {
            await this.auditWriter.WriteAsync("UpdateApplication", command.ActorId, application.Id.Value.ToString(), "Denied", cancellationToken).ConfigureAwait(false);
            return DomainError.Forbidden("ApplicationPermission.Forbidden", "Actor cannot manage this registered application.");
        }

        if (command.ExpectedConcurrencyToken.HasValue && application.ConcurrencyToken != command.ExpectedConcurrencyToken.Value)
        {
            return DomainError.Conflict("ApplicationPermission.ConcurrencyConflict", "The registered application was modified by another request.");
        }

        bool isManifestBacked = !string.IsNullOrWhiteSpace(application.ManifestBaseUrl);
        if (!isManifestBacked)
        {
            if (!string.IsNullOrWhiteSpace(command.ManifestBaseUrl))
            {
                await this.auditWriter.WriteAsync("UpdateApplication", command.ActorId, application.Id.Value.ToString(), ManualApplicationCannotBeManifestBacked.Code, cancellationToken).ConfigureAwait(false);
                return ManualApplicationCannotBeManifestBacked;
            }

            Result updateResult = application.UpdateMetadata(command.DisplayName, command.Description, command.ActorId, this.dateTimeProvider);
            if (updateResult.IsFailure)
            {
                await this.auditWriter.WriteAsync("UpdateApplication", command.ActorId, application.Id.Value.ToString(), updateResult.Error.Code, cancellationToken).ConfigureAwait(false);
                return updateResult.Error;
            }

            await this.repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await this.auditWriter.WriteAsync("UpdateApplication", command.ActorId, application.Id.Value.ToString(), "Succeeded", cancellationToken).ConfigureAwait(false);
            return MapToDto(application);
        }

        if (ApplicationMetadataChanged(application, command))
        {
            await this.auditWriter.WriteAsync("UpdateApplication", command.ActorId, application.Id.Value.ToString(), ManifestBackedApplicationReadOnly.Code, cancellationToken).ConfigureAwait(false);
            return ManifestBackedApplicationReadOnly;
        }

        string? normalizedManifestBaseUrl = NormalizeManifestBaseUrl(command.ManifestBaseUrl);
        if (string.IsNullOrWhiteSpace(normalizedManifestBaseUrl))
        {
            return DomainError.Validation("RegisteredApplication.ManifestBaseUrlRequired", "Manifest base URL is required.");
        }

        Result urlValidation = ValidateManifestBaseUrl(normalizedManifestBaseUrl);
        if (urlValidation.IsFailure)
        {
            return urlValidation.Error;
        }

        if (!string.Equals(application.ManifestBaseUrl, normalizedManifestBaseUrl, StringComparison.OrdinalIgnoreCase)
            && await this.repository.ExistsByManifestBaseUrlAsync(normalizedManifestBaseUrl, cancellationToken).ConfigureAwait(false))
        {
            return DomainError.Conflict("PermissionManifest.ManifestBaseUrlConflict", "Manifest base URL is already trusted by another registered application.");
        }

        Result manifestUrlUpdate = application.UpdateManifestBaseUrl(normalizedManifestBaseUrl, command.ActorId, this.dateTimeProvider);
        if (manifestUrlUpdate.IsFailure)
        {
            await this.auditWriter.WriteAsync("UpdateApplication", command.ActorId, application.Id.Value.ToString(), manifestUrlUpdate.Error.Code, cancellationToken).ConfigureAwait(false);
            return manifestUrlUpdate.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await this.auditWriter.WriteAsync("UpdateApplication", command.ActorId, application.Id.Value.ToString(), "Succeeded", cancellationToken).ConfigureAwait(false);
        return MapToDto(application);
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

    public async Task<Result<DestructiveOperationResultDto>> ExecuteAsync(DeleteApplicationPermissionCommand command, CancellationToken cancellationToken = default)
    {
        return await this.transactionRunner
            .ExecuteAsync(token => this.DeleteApplicationPermissionCoreAsync(command, token), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<DeletionImpactDto>> ExecuteAsync(PreviewDeleteApplicationPermissionCommand command, CancellationToken cancellationToken = default)
    {
        RegisteredApplication? application = await this.repository
            .GetByPermissionIdAsync(new ApplicationPermissionId(command.PermissionId), cancellationToken)
            .ConfigureAwait(false);
        if (application is null)
        {
            return DomainError.NotFound("ApplicationPermission.NotFound", $"Permission '{command.PermissionId}' not found.");
        }

        ApplicationPermission? permission = application.Permissions.FirstOrDefault(candidate => candidate.Id.Value == command.PermissionId && !candidate.IsRemoved);
        if (permission is null)
        {
            return DomainError.NotFound("ApplicationPermission.NotFound", $"Permission '{command.PermissionId}' not found.");
        }

        Result adminResult = await this.RequireRegistryAdminAsync(command.ActorId, application, "PreviewDeletePermission", cancellationToken).ConfigureAwait(false);
        if (adminResult.IsFailure)
        {
            return adminResult.Error;
        }

        var remaining = application.Permissions.Where(candidate => !candidate.IsRemoved && candidate.Id != permission.Id).ToList();
        var plan = new PermissionAssignmentRemovalPlan(
            [permission.FullPermissionKey],
            BuildCollapsedWildcardPermissions(application, [permission], remaining));
        IReadOnlyList<PermissionAssignmentImpactDto> impacts = await this.permissionAssignmentStore
            .PreviewRemovalImpactAsync(plan, cancellationToken)
            .ConfigureAwait(false);

        return new DeletionImpactDto(command.PermissionId, "permission", impacts, []);
    }

    private async Task<Result<DestructiveOperationResultDto>> DeleteApplicationPermissionCoreAsync(DeleteApplicationPermissionCommand command, CancellationToken cancellationToken)
    {
        RegisteredApplication? application = await this.repository
            .GetByPermissionIdAsync(new ApplicationPermissionId(command.PermissionId), cancellationToken)
            .ConfigureAwait(false);
        if (application is null)
        {
            return DomainError.NotFound("ApplicationPermission.NotFound", $"Permission '{command.PermissionId}' not found.");
        }

        ApplicationPermission? permission = application.Permissions.FirstOrDefault(candidate => candidate.Id.Value == command.PermissionId && !candidate.IsRemoved);
        if (permission is null)
        {
            return DomainError.NotFound("ApplicationPermission.NotFound", $"Permission '{command.PermissionId}' not found.");
        }

        Result adminResult = await this.RequireRegistryAdminAsync(command.ActorId, application, "DeletePermission", cancellationToken).ConfigureAwait(false);
        if (adminResult.IsFailure)
        {
            return adminResult.Error;
        }

        if (command.ExpectedConcurrencyToken.HasValue && application.ConcurrencyToken != command.ExpectedConcurrencyToken.Value)
        {
            return DomainError.Conflict("ApplicationPermission.ConcurrencyConflict", "The registered application was modified by another request.");
        }

        if (!string.IsNullOrWhiteSpace(application.ManifestBaseUrl))
        {
            await this.auditWriter.WriteAsync("DeletePermission", command.ActorId, application.Id.Value.ToString(), ManifestBackedApplicationReadOnly.Code, cancellationToken).ConfigureAwait(false);
            return ManifestBackedApplicationReadOnly;
        }

        var remaining = application.Permissions.Where(candidate => !candidate.IsRemoved && candidate.Id != permission.Id).ToList();
        var plan = new PermissionAssignmentRemovalPlan(
            [permission.FullPermissionKey],
            BuildCollapsedWildcardPermissions(application, [permission], remaining));
        Result<IReadOnlyList<PermissionAssignmentImpactDto>> assignmentResult = await this.permissionAssignmentStore
            .RemoveAssignmentsAsync(plan, command.ActorId, cancellationToken)
            .ConfigureAwait(false);
        if (assignmentResult.IsFailure)
        {
            return assignmentResult.Error;
        }

        Result removeResult = application.RemovePermission(permission.Id, command.ActorId, command.Reason, this.dateTimeProvider);
        if (removeResult.IsFailure)
        {
            return removeResult.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await this.auditWriter.WriteAsync("DeletePermission", command.ActorId, application.Id.Value.ToString(), "Succeeded", cancellationToken).ConfigureAwait(false);
        return BuildDestructiveResult(application, [permission], assignmentResult.Value, metadataUpdated: false, manifestVersionAdvanced: false);
    }

    public async Task<Result<DestructiveOperationResultDto>> ExecuteAsync(DeleteRegisteredApplicationCommand command, CancellationToken cancellationToken = default)
    {
        return await this.transactionRunner
            .ExecuteAsync(token => this.DeleteRegisteredApplicationCoreAsync(command, token), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<DeletionImpactDto>> ExecuteAsync(PreviewDeleteRegisteredApplicationCommand command, CancellationToken cancellationToken = default)
    {
        RegisteredApplication? application = await this.repository.GetByIdAsync(new RegisteredApplicationId(command.ApplicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return DomainError.NotFound("RegisteredApplication.NotFound", $"Application '{command.ApplicationId}' not found.");
        }

        Result adminResult = await this.RequireRegistryAdminAsync(command.ActorId, application, "PreviewDeleteApplication", cancellationToken).ConfigureAwait(false);
        if (adminResult.IsFailure)
        {
            return adminResult.Error;
        }

        var removals = application.Permissions.Where(static permission => !permission.IsRemoved).ToList();
        var plan = new PermissionAssignmentRemovalPlan(
            removals.Select(permission => permission.FullPermissionKey).ToList(),
            BuildApplicationWildcardPermissions(application, removals));
        IReadOnlyList<PermissionAssignmentImpactDto> impacts = await this.permissionAssignmentStore
            .PreviewRemovalImpactAsync(plan, cancellationToken)
            .ConfigureAwait(false);

        return new DeletionImpactDto(command.ApplicationId, "application", impacts, []);
    }

    private async Task<Result<DestructiveOperationResultDto>> DeleteRegisteredApplicationCoreAsync(DeleteRegisteredApplicationCommand command, CancellationToken cancellationToken)
    {
        RegisteredApplication? application = await this.repository.GetByIdAsync(new RegisteredApplicationId(command.ApplicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return DomainError.NotFound("RegisteredApplication.NotFound", $"Application '{command.ApplicationId}' not found.");
        }

        Result adminResult = await this.RequireRegistryAdminAsync(command.ActorId, application, "DeleteApplication", cancellationToken).ConfigureAwait(false);
        if (adminResult.IsFailure)
        {
            return adminResult.Error;
        }

        if (command.ExpectedConcurrencyToken.HasValue && application.ConcurrencyToken != command.ExpectedConcurrencyToken.Value)
        {
            return DomainError.Conflict("ApplicationPermission.ConcurrencyConflict", "The registered application was modified by another request.");
        }

        var removals = application.Permissions.Where(static permission => !permission.IsRemoved).ToList();
        var plan = new PermissionAssignmentRemovalPlan(
            removals.Select(permission => permission.FullPermissionKey).ToList(),
            BuildApplicationWildcardPermissions(application, removals));
        Result<IReadOnlyList<PermissionAssignmentImpactDto>> assignmentResult = await this.permissionAssignmentStore
            .RemoveAssignmentsAsync(plan, command.ActorId, cancellationToken)
            .ConfigureAwait(false);
        if (assignmentResult.IsFailure)
        {
            return assignmentResult.Error;
        }

        application.MarkDeleted(command.ActorId, command.Reason, this.dateTimeProvider);

        await this.repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await this.auditWriter.WriteAsync("DeleteApplication", command.ActorId, application.Id.Value.ToString(), "Succeeded", cancellationToken).ConfigureAwait(false);
        return BuildDestructiveResult(application, removals, assignmentResult.Value, metadataUpdated: false, manifestVersionAdvanced: false);
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

    public async Task<Result<ApplicationPermissionHistoryDto>> HandleAsync(ListApplicationPermissionHistoryQuery query, CancellationToken cancellationToken = default)
    {
        return await this.ListHistoryAsync(query, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PermissionDiagnosticsDto>> HandleAsync(ListApplicationPermissionDiagnosticsQuery query, CancellationToken cancellationToken = default)
    {
        if (!await this.authorizationService.CanAdministerRegistryAsync(query.ActorId, cancellationToken).ConfigureAwait(false))
        {
            await this.auditWriter.WriteAsync("ListPermissionDiagnostics", query.ActorId, null, "Denied", cancellationToken).ConfigureAwait(false);
            return DomainError.Forbidden("ApplicationPermission.Forbidden", "Actor cannot administer application permissions.");
        }

        if (this.diagnosticsReader is null)
        {
            return DomainError.Validation("ApplicationPermission.DiagnosticsUnavailable", "Permission diagnostics reader is not configured.");
        }

        return await this.diagnosticsReader.ListIssuesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ApplicationPermissionHistoryDto>> ListHistoryAsync(ListApplicationPermissionHistoryQuery query, CancellationToken cancellationToken = default)
    {
        if (!await this.authorizationService.CanAdministerRegistryAsync(query.ActorId, cancellationToken).ConfigureAwait(false))
        {
            await this.auditWriter.WriteAsync("ListPermissionHistory", query.ActorId, null, "Denied", cancellationToken).ConfigureAwait(false);
            return DomainError.Forbidden("ApplicationPermission.Forbidden", "Actor cannot administer application permissions.");
        }

        return await this.repository
            .ListHistoryAsync(query.ApplicationIdentifier, query.IncludeApplications, query.IncludePermissions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<RemovedPermissionDetailDto>> ExecuteAsync(UpdateRemovedPermissionReplacementCommand command, CancellationToken cancellationToken = default)
    {
        return await this.UpdateReplacementGuidanceAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RemovedPermissionDetailDto>> UpdateReplacementGuidanceAsync(UpdateRemovedPermissionReplacementCommand command, CancellationToken cancellationToken = default)
    {
        RegisteredApplication? application = await this.repository
            .GetByPermissionIdAsync(new ApplicationPermissionId(command.PermissionId), cancellationToken)
            .ConfigureAwait(false);
        if (application is null)
        {
            return DomainError.NotFound("ApplicationPermission.NotFound", $"Permission '{command.PermissionId}' not found.");
        }

        ApplicationPermission? permission = application.Permissions.FirstOrDefault(candidate => candidate.Id.Value == command.PermissionId);
        if (permission is null)
        {
            return DomainError.NotFound("ApplicationPermission.NotFound", $"Permission '{command.PermissionId}' not found.");
        }

        Result adminResult = await this.RequireRegistryAdminAsync(command.ActorId, application, "UpdatePermissionReplacement", cancellationToken).ConfigureAwait(false);
        if (adminResult.IsFailure)
        {
            return adminResult.Error;
        }

        if (command.ExpectedConcurrencyToken.HasValue && application.ConcurrencyToken != command.ExpectedConcurrencyToken.Value)
        {
            return DomainError.Conflict("ApplicationPermission.ConcurrencyConflict", "The registered application was modified by another request.");
        }

        if (!string.IsNullOrWhiteSpace(application.ManifestBaseUrl))
        {
            await this.auditWriter.WriteAsync("UpdatePermissionReplacement", command.ActorId, application.Id.Value.ToString(), ManifestBackedApplicationReadOnly.Code, cancellationToken).ConfigureAwait(false);
            return ManifestBackedApplicationReadOnly;
        }

        if (!await this.ReplacementExistsAsync(command.ReplacementFullPermissionKey, cancellationToken).ConfigureAwait(false))
        {
            return DomainError.Validation("ApplicationPermission.ReplacementNotFound", "Replacement guidance must point to a current platform or application permission.");
        }

        Result result = application.SetPermissionReplacementGuidance(
            permission.Id,
            command.ReplacementFullPermissionKey,
            command.ReplacementNote,
            command.ActorId,
            this.dateTimeProvider);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await this.auditWriter.WriteAsync("UpdatePermissionReplacement", command.ActorId, application.Id.Value.ToString(), "Succeeded", cancellationToken).ConfigureAwait(false);
        return MapRemovedPermission(application, permission);
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
        if (!await this.authorizationService.CanManageApplicationAsync(actorId, application, cancellationToken).ConfigureAwait(false))
        {
            await this.auditWriter.WriteAsync(action, actorId, application.Id.Value.ToString(), "Denied", cancellationToken).ConfigureAwait(false);
            return DomainError.Forbidden("ApplicationPermission.Forbidden", "Actor cannot manage this registered application.");
        }

        if (expectedConcurrencyToken.HasValue && application.ConcurrencyToken != expectedConcurrencyToken.Value)
        {
            return DomainError.Conflict("ApplicationPermission.ConcurrencyConflict", "The registered application was modified by another request.");
        }

        if (!string.IsNullOrWhiteSpace(application.ManifestBaseUrl))
        {
            await this.auditWriter.WriteAsync(action, actorId, application.Id.Value.ToString(), ManifestBackedApplicationReadOnly.Code, cancellationToken).ConfigureAwait(false);
            return ManifestBackedApplicationReadOnly;
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

    private async Task<bool> ReplacementExistsAsync(string fullPermissionKey, CancellationToken cancellationToken)
    {
        string normalized = fullPermissionKey.Trim().ToLowerInvariant();
        if (Permissions.GetAllPermissions().Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        ApplicationPermission? permission = await this.repository.GetPermissionByFullKeyAsync(normalized, cancellationToken).ConfigureAwait(false);
        return permission is { IsRemoved: false };
    }

    private static bool ApplicationMetadataChanged(RegisteredApplication application, UpdateRegisteredApplicationCommand command)
    {
        return !string.Equals(application.DisplayName, command.DisplayName.Trim(), StringComparison.Ordinal)
            || !string.Equals(application.Description ?? string.Empty, command.Description?.Trim() ?? string.Empty, StringComparison.Ordinal);
    }

    private static Result ValidateManifestBaseUrl(string manifestBaseUrl)
    {
        if (!Uri.TryCreate(manifestBaseUrl, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            return ApplicationPermissionManifestValidator.ManifestBaseUrlInvalid;
        }

        return Result.Success();
    }

    private static string? NormalizeManifestBaseUrl(string? manifestBaseUrl)
    {
        return string.IsNullOrWhiteSpace(manifestBaseUrl)
            ? null
            : manifestBaseUrl.Trim().TrimEnd('/');
    }

    private async Task<Result> RequireRegistryAdminAsync(
        string actorId,
        RegisteredApplication application,
        string action,
        CancellationToken cancellationToken)
    {
        if (await this.authorizationService.CanAdministerRegistryAsync(actorId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Success();
        }

        await this.auditWriter.WriteAsync(action, actorId, application.Id.Value.ToString(), "Denied", cancellationToken).ConfigureAwait(false);
        return DomainError.Forbidden("ApplicationPermission.Forbidden", "Actor cannot administer application permissions.");
    }

    private static List<string> BuildCollapsedWildcardPermissions(
        RegisteredApplication application,
        IReadOnlyList<ApplicationPermission> removals,
        IReadOnlyList<ApplicationPermission> remaining)
    {
        return removals
            .Select(permission => permission.PermissionKey.Split(':', 2)[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(resource => !remaining.Any(permission => permission.PermissionKey.StartsWith($"{resource}:", StringComparison.OrdinalIgnoreCase)))
            .Select(resource => $"{application.ApplicationIdentifier}:{resource}:*")
            .ToList();
    }

    private static List<string> BuildApplicationWildcardPermissions(
        RegisteredApplication application,
        IReadOnlyList<ApplicationPermission> removals)
    {
        return removals
            .Select(permission => permission.PermissionKey.Split(':', 2)[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(resource => $"{application.ApplicationIdentifier}:{resource}:*")
            .ToList();
    }

    private static DestructiveOperationResultDto BuildDestructiveResult(
        RegisteredApplication application,
        IReadOnlyList<ApplicationPermission> removedPermissions,
        IReadOnlyList<PermissionAssignmentImpactDto> assignmentImpacts,
        bool metadataUpdated,
        bool manifestVersionAdvanced)
    {
        return new DestructiveOperationResultDto(
            removedPermissions.Select(permission => ToPermissionDto(application, permission)).ToList(),
            assignmentImpacts.Where(static impact => impact.ImpactKind == "exactRemoved").ToList(),
            assignmentImpacts.Where(static impact => impact.ImpactKind == "wildcardRemoved").ToList(),
            assignmentImpacts.Where(static impact => impact.ImpactKind == "wildcardImpacted").ToList(),
            metadataUpdated,
            manifestVersionAdvanced);
    }

    public static DeletedApplicationHistoryDto MapSummary(RegisteredApplication application)
    {
        return new DeletedApplicationHistoryDto(
            application.Id.Value,
            application.ApplicationIdentifier,
            application.DisplayName,
            application.OwnerId,
            application.OwnerType.ToString(),
            application.Status.ToString(),
            application.Permissions.Count(static permission => !permission.IsRemoved),
            application.CreatedAt,
            application.ModifiedAt,
            application.DeletedAt ?? application.ModifiedAt ?? application.CreatedAt,
            application.DeletedBy ?? string.Empty,
            application.DeleteReason ?? string.Empty,
            application.ConcurrencyToken);
    }

    public static RemovedPermissionDetailDto MapRemovedPermission(RegisteredApplication application, ApplicationPermission permission)
    {
        return new RemovedPermissionDetailDto(
            permission.Id.Value,
            application.Id.Value,
            application.ApplicationIdentifier,
            permission.PermissionKey,
            permission.FullPermissionKey,
            permission.DisplayName,
            permission.Description,
            permission.Category,
            permission.CreatedAt,
            permission.ModifiedAt,
            permission.RemovedAt ?? permission.ModifiedAt ?? permission.CreatedAt,
            permission.RemovedBy ?? string.Empty,
            permission.RemoveReason ?? string.Empty,
            permission.ReplacementFullPermissionKey,
            permission.ReplacementNote,
            application.ConcurrencyToken);
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

    private static ApplicationPermissionDto ToPermissionDto(RegisteredApplication application, ApplicationPermission permission)
    {
        return new ApplicationPermissionDto(
            permission.Id.Value,
            permission.PermissionKey,
            permission.FullPermissionKey,
            permission.DisplayName,
            permission.Description,
            permission.Category,
            permission.CreatedAt,
            permission.ModifiedAt,
            application.ApplicationIdentifier,
            application.DisplayName,
            application.ManifestVersion,
            RemovedAt: permission.RemovedAt,
            RemovedBy: permission.RemovedBy,
            RemoveReason: permission.RemoveReason,
            ReplacementFullPermissionKey: permission.ReplacementFullPermissionKey,
            ReplacementNote: permission.ReplacementNote,
            ConcurrencyToken: application.ConcurrencyToken);
    }

}
