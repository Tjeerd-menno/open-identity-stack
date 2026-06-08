using OpenIdentityStack.Application.ApplicationPermissions.Commands;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;

namespace OpenIdentityStack.Application.ApplicationPermissions;

public interface IApplicationPermissionRegistryWorkflow
{
    Task<Result<RegisterApplicationResult>> RegisterManualApplicationAsync(
        RegisterManualApplicationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<RegisteredApplicationDto>> ImportManifestAsync(
        ImportManifestRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ManifestPreviewDto>> PreviewManifestAsync(
        PreviewManifestRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ManifestApplyDto>> ApplyManifestAsync(
        ApplyManifestRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<RegisteredApplicationDto>> UpdateApplicationAsync(
        UpdateApplicationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<RegisteredApplicationDto>> AddPermissionAsync(
        AddPermissionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<RegisteredApplicationDto>> UpdatePermissionAsync(
        UpdatePermissionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<RegisteredApplicationDto>> ChangeLifecycleAsync(
        ChangeLifecycleRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<RegisteredApplicationDto>> TransferOwnershipAsync(
        TransferOwnershipRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<RegisteredApplicationDto>> AddMaintainerAsync(
        AddMaintainerRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<RegisteredApplicationDto>> RemoveMaintainerAsync(
        RemoveMaintainerRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<DeletionImpactDto>> PreviewDeletePermissionAsync(
        PreviewDeletePermissionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<DestructiveOperationResultDto>> DeletePermissionAsync(
        DeletePermissionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<DeletionImpactDto>> PreviewDeleteApplicationAsync(
        PreviewDeleteApplicationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<DestructiveOperationResultDto>> DeleteApplicationAsync(
        DeleteApplicationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<RemovedPermissionDetailDto>> UpdateRemovedPermissionReplacementAsync(
        UpdateRemovedPermissionReplacementRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ApplicationPermissionHistoryDto>> ListHistoryAsync(
        ListHistoryRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PermissionDiagnosticsDto>> ListDiagnosticsAsync(
        ListDiagnosticsRequest request,
        CancellationToken cancellationToken = default);
}
