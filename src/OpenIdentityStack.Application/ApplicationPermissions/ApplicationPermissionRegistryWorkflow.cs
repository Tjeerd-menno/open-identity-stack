using OpenIdentityStack.Application.ApplicationPermissions.Commands;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Application.ApplicationPermissions.Queries;
using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Application.ApplicationPermissions;

public sealed class ApplicationPermissionRegistryWorkflow : IApplicationPermissionRegistryWorkflow
{
    private static readonly DomainError manifestRequired = DomainError.Validation(
        "PermissionManifest.ManifestRequired",
        "Manifest is required.");

    private readonly RegisterApplicationUseCase registerApplicationUseCase;
    private readonly ApplicationPermissionManifestUseCases manifestUseCases;
    private readonly ApplicationPermissionMaintenanceUseCases maintenanceUseCases;

    public ApplicationPermissionRegistryWorkflow(
        RegisterApplicationUseCase registerApplicationUseCase,
        ApplicationPermissionManifestUseCases manifestUseCases,
        ApplicationPermissionMaintenanceUseCases maintenanceUseCases)
    {
        this.registerApplicationUseCase = registerApplicationUseCase;
        this.manifestUseCases = manifestUseCases;
        this.maintenanceUseCases = maintenanceUseCases;
    }

    public async Task<Result<RegisterApplicationResult>> RegisterManualApplicationAsync(
        RegisterManualApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        return await this.registerApplicationUseCase.ExecuteAsync(
            new RegisterApplicationCommand(
                request.ApplicationIdentifier,
                request.DisplayName,
                request.Description,
                request.OwnerId,
                request.OwnerType.ToString(),
                request.ActorId,
                request.Permissions),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredApplicationDto>> ImportManifestAsync(
        ImportManifestRequest request,
        CancellationToken cancellationToken = default)
    {
        return await this.manifestUseCases.CreateAsync(
            new CreateApplicationPermissionManifestCommand(
                request.Manifest,
                request.OwnerId,
                request.OwnerType,
                request.ManifestBaseUrl,
                request.ActorId,
                request.IsImported),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ManifestPreviewDto>> PreviewManifestAsync(
        PreviewManifestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.FetchRemote)
        {
            return await this.manifestUseCases.PreviewRemoteChangesAsync(
                new RemoteApplicationPermissionManifestCommand(
                    request.ApplicationId,
                    request.ActorId,
                    request.ExpectedConcurrencyToken),
                cancellationToken).ConfigureAwait(false);
        }

        if (request.Manifest is null)
        {
            return manifestRequired;
        }

        return await this.manifestUseCases.PreviewChangesAsync(
            new ApplyApplicationPermissionManifestCommand(
                request.ApplicationId,
                request.Manifest,
                request.ActorId,
                request.ExpectedConcurrencyToken),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ManifestApplyDto>> ApplyManifestAsync(
        ApplyManifestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.FetchRemote)
        {
            return await this.manifestUseCases.ApplyRemoteChangesAsync(
                new RemoteApplicationPermissionManifestCommand(
                    request.ApplicationId,
                    request.ActorId,
                    request.ExpectedConcurrencyToken),
                cancellationToken).ConfigureAwait(false);
        }

        if (request.Manifest is null)
        {
            return manifestRequired;
        }

        return await this.manifestUseCases.ApplyChangesAsync(
            new ApplyApplicationPermissionManifestCommand(
                request.ApplicationId,
                request.Manifest,
                request.ActorId,
                request.ExpectedConcurrencyToken),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredApplicationDto>> UpdateApplicationAsync(
        UpdateApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        return await this.maintenanceUseCases.ExecuteAsync(
            new UpdateRegisteredApplicationCommand(
                request.ApplicationId,
                request.DisplayName,
                request.Description,
                request.ActorId,
                request.ExpectedConcurrencyToken,
                request.ManifestBaseUrl),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredApplicationDto>> AddPermissionAsync(
        AddPermissionRequest request,
        CancellationToken cancellationToken = default)
    {
        return await this.maintenanceUseCases.ExecuteAsync(
            new AddApplicationPermissionCommand(
                request.ApplicationId,
                request.PermissionKey,
                request.DisplayName,
                request.Description,
                request.Category,
                request.ActorId,
                request.ExpectedConcurrencyToken),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredApplicationDto>> UpdatePermissionAsync(
        UpdatePermissionRequest request,
        CancellationToken cancellationToken = default)
    {
        return await this.maintenanceUseCases.ExecuteAsync(
            new UpdateApplicationPermissionCommand(
                request.PermissionId,
                request.DisplayName,
                request.Description,
                request.Category,
                request.ActorId,
                request.ExpectedConcurrencyToken),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredApplicationDto>> ChangeLifecycleAsync(
        ChangeLifecycleRequest request,
        CancellationToken cancellationToken = default)
    {
        return await this.maintenanceUseCases.ExecuteAsync(
            new ChangeRegisteredApplicationLifecycleCommand(
                request.ApplicationId,
                request.Status,
                request.ActorId,
                request.AcknowledgeDependencies,
                request.ExpectedConcurrencyToken),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredApplicationDto>> TransferOwnershipAsync(
        TransferOwnershipRequest request,
        CancellationToken cancellationToken = default)
    {
        return await this.maintenanceUseCases.ExecuteAsync(
            new TransferRegisteredApplicationOwnershipCommand(
                request.ApplicationId,
                request.OwnerId,
                request.OwnerType,
                request.ActorId,
                request.ExpectedConcurrencyToken),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredApplicationDto>> AddMaintainerAsync(
        AddMaintainerRequest request,
        CancellationToken cancellationToken = default)
    {
        return await this.maintenanceUseCases.ExecuteAsync(
            new AddDelegatedMaintainerCommand(
                request.ApplicationId,
                request.PrincipalId,
                request.PrincipalType,
                request.ActorId,
                request.ExpectedConcurrencyToken),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RegisteredApplicationDto>> RemoveMaintainerAsync(
        RemoveMaintainerRequest request,
        CancellationToken cancellationToken = default)
    {
        return await this.maintenanceUseCases.ExecuteAsync(
            new RemoveDelegatedMaintainerCommand(
                request.ApplicationId,
                request.PrincipalId,
                request.ActorId,
                request.ExpectedConcurrencyToken),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<DeletionImpactDto>> PreviewDeletePermissionAsync(
        PreviewDeletePermissionRequest request,
        CancellationToken cancellationToken = default)
    {
        return await this.maintenanceUseCases.ExecuteAsync(
            new PreviewDeleteApplicationPermissionCommand(request.PermissionId, request.ActorId),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<DestructiveOperationResultDto>> DeletePermissionAsync(
        DeletePermissionRequest request,
        CancellationToken cancellationToken = default)
    {
        return await this.maintenanceUseCases.ExecuteAsync(
            new DeleteApplicationPermissionCommand(
                request.PermissionId,
                request.Reason,
                request.ActorId,
                request.ExpectedConcurrencyToken),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<DeletionImpactDto>> PreviewDeleteApplicationAsync(
        PreviewDeleteApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        return await this.maintenanceUseCases.ExecuteAsync(
            new PreviewDeleteRegisteredApplicationCommand(request.ApplicationId, request.ActorId),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<DestructiveOperationResultDto>> DeleteApplicationAsync(
        DeleteApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        return await this.maintenanceUseCases.ExecuteAsync(
            new DeleteRegisteredApplicationCommand(
                request.ApplicationId,
                request.Reason,
                request.ActorId,
                request.ExpectedConcurrencyToken),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RemovedPermissionDetailDto>> UpdateRemovedPermissionReplacementAsync(
        UpdateRemovedPermissionReplacementRequest request,
        CancellationToken cancellationToken = default)
    {
        return await this.maintenanceUseCases.ExecuteAsync(
            new UpdateRemovedPermissionReplacementCommand(
                request.PermissionId,
                request.ReplacementFullPermissionKey,
                request.ReplacementNote,
                request.ActorId,
                request.ExpectedConcurrencyToken),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ApplicationPermissionHistoryDto>> ListHistoryAsync(
        ListHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        return await this.maintenanceUseCases.ListHistoryAsync(
            new ListApplicationPermissionHistoryQuery(
                request.ApplicationIdentifier,
                request.IncludeApplications,
                request.IncludePermissions,
                request.ActorId),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PermissionDiagnosticsDto>> ListDiagnosticsAsync(
        ListDiagnosticsRequest request,
        CancellationToken cancellationToken = default)
    {
        return await this.maintenanceUseCases.HandleAsync(
            new ListApplicationPermissionDiagnosticsQuery(request.ActorId),
            cancellationToken).ConfigureAwait(false);
    }
}
