using OpenIdentityStack.Application.ApplicationPermissions;
using OpenIdentityStack.Application.ApplicationPermissions.Commands;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Application.ApplicationPermissions.Validators;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.Tests.ApplicationPermissions;

public sealed class ApplicationPermissionRegistryWorkflowContractTests
{
    [Fact]
    public async Task InterfaceGroupsRegistryOperatorActionsBehindOneDependency()
    {
        IApplicationPermissionRegistryWorkflow workflow = new ContractWorkflow();
        PermissionManifestDocument manifest = CreateManifest();
        var applicationId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        Result<RegisterApplicationResult> manualRegistration = await workflow.RegisterManualApplicationAsync(
            new RegisterManualApplicationRequest(
                "orders-api",
                "Orders API",
                "Order permissions",
                "owner-1",
                OwnerType.User,
                "actor-1",
                [new RegisterApplicationPermissionInput("orders:read", "Read orders")]));
        Result<RegisteredApplicationDto> imported = await workflow.ImportManifestAsync(
            new ImportManifestRequest(manifest, "owner-1", OwnerType.User, "https://orders.example.com", "actor-1", IsImported: true));
        Result<ManifestPreviewDto> preview = await workflow.PreviewManifestAsync(
            new PreviewManifestRequest(applicationId, manifest, "actor-1", 1));
        Result<ManifestApplyDto> apply = await workflow.ApplyManifestAsync(
            new ApplyManifestRequest(applicationId, manifest, "actor-1", 1));
        Result<RegisteredApplicationDto> update = await workflow.UpdateApplicationAsync(
            new UpdateApplicationRequest(applicationId, "Orders API", "Updated", "actor-1", 1, "https://orders.example.com"));
        Result<RegisteredApplicationDto> addPermission = await workflow.AddPermissionAsync(
            new AddPermissionRequest(applicationId, "orders:write", "Write orders", null, "Orders", "actor-1", 1));
        Result<RegisteredApplicationDto> updatePermission = await workflow.UpdatePermissionAsync(
            new UpdatePermissionRequest(permissionId, "Write orders", "Updated", "Orders", "actor-1", 1));
        Result<RegisteredApplicationDto> lifecycle = await workflow.ChangeLifecycleAsync(
            new ChangeLifecycleRequest(applicationId, ApplicationLifecycleStatus.Disabled, "actor-1", true, 1));
        Result<RegisteredApplicationDto> ownership = await workflow.TransferOwnershipAsync(
            new TransferOwnershipRequest(applicationId, "owner-2", OwnerType.Group, "actor-1", 1));
        Result<RegisteredApplicationDto> addedMaintainer = await workflow.AddMaintainerAsync(
            new AddMaintainerRequest(applicationId, "maintainer-1", OwnerType.User, "actor-1", 1));
        Result<RegisteredApplicationDto> removedMaintainer = await workflow.RemoveMaintainerAsync(
            new RemoveMaintainerRequest(applicationId, "maintainer-1", "actor-1", 1));
        Result<DeletionImpactDto> permissionPreview = await workflow.PreviewDeletePermissionAsync(
            new PreviewDeletePermissionRequest(permissionId, "actor-1"));
        Result<DestructiveOperationResultDto> permissionDelete = await workflow.DeletePermissionAsync(
            new DeletePermissionRequest(permissionId, "Retired.", "actor-1", 1));
        Result<DeletionImpactDto> applicationPreview = await workflow.PreviewDeleteApplicationAsync(
            new PreviewDeleteApplicationRequest(applicationId, "actor-1"));
        Result<DestructiveOperationResultDto> applicationDelete = await workflow.DeleteApplicationAsync(
            new DeleteApplicationRequest(applicationId, "Retired.", "actor-1", 1));
        Result<RemovedPermissionDetailDto> replacement = await workflow.UpdateRemovedPermissionReplacementAsync(
            new UpdateRemovedPermissionReplacementRequest(permissionId, "orders-api:orders:read", "Use read.", "actor-1", 1));
        Result<ApplicationPermissionHistoryDto> history = await workflow.ListHistoryAsync(
            new ListHistoryRequest("orders-api", IncludeApplications: true, IncludePermissions: true, "actor-1"));
        Result<PermissionDiagnosticsDto> diagnostics = await workflow.ListDiagnosticsAsync(
            new ListDiagnosticsRequest("actor-1"));

        manualRegistration.IsSuccess.ShouldBeTrue();
        imported.IsSuccess.ShouldBeTrue();
        preview.IsSuccess.ShouldBeTrue();
        apply.IsSuccess.ShouldBeTrue();
        update.IsSuccess.ShouldBeTrue();
        addPermission.IsSuccess.ShouldBeTrue();
        updatePermission.IsSuccess.ShouldBeTrue();
        lifecycle.IsSuccess.ShouldBeTrue();
        ownership.IsSuccess.ShouldBeTrue();
        addedMaintainer.IsSuccess.ShouldBeTrue();
        removedMaintainer.IsSuccess.ShouldBeTrue();
        permissionPreview.IsSuccess.ShouldBeTrue();
        permissionDelete.IsSuccess.ShouldBeTrue();
        applicationPreview.IsSuccess.ShouldBeTrue();
        applicationDelete.IsSuccess.ShouldBeTrue();
        replacement.IsSuccess.ShouldBeTrue();
        history.IsSuccess.ShouldBeTrue();
        diagnostics.IsSuccess.ShouldBeTrue();
    }

    private static PermissionManifestDocument CreateManifest()
    {
        return new PermissionManifestDocument(
            "1.0.0",
            new PermissionManifestApplicationDeclaration("orders-api", "Orders API", "Order permissions", "1.0.0"),
            [new PermissionManifestPermissionDeclaration("orders:read", "Read orders", null, "Orders")]);
    }

    private sealed class ContractWorkflow : IApplicationPermissionRegistryWorkflow
    {
        public Task<Result<RegisterApplicationResult>> RegisterManualApplicationAsync(RegisterManualApplicationRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Result<RegisterApplicationResult>)new RegisterApplicationResult(Guid.NewGuid(), request.ApplicationIdentifier, request.Permissions.Count));
        }

        public Task<Result<RegisteredApplicationDto>> ImportManifestAsync(ImportManifestRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Result<RegisteredApplicationDto>)CreateApplicationDto());
        }

        public Task<Result<ManifestPreviewDto>> PreviewManifestAsync(PreviewManifestRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Result<ManifestPreviewDto>)new ManifestPreviewDto(request.ApplicationId, "1.0.0", "1.1.0", HasChanges: true, VersionWillAdvance: true, IsDestructive: false, [], [], [], []));
        }

        public Task<Result<ManifestApplyDto>> ApplyManifestAsync(ApplyManifestRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Result<ManifestApplyDto>)new ManifestApplyDto(CreateApplicationDto(), CreateDestructiveResult()));
        }

        public Task<Result<RegisteredApplicationDto>> UpdateApplicationAsync(UpdateApplicationRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Result<RegisteredApplicationDto>)CreateApplicationDto());
        }

        public Task<Result<RegisteredApplicationDto>> AddPermissionAsync(AddPermissionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Result<RegisteredApplicationDto>)CreateApplicationDto());
        }

        public Task<Result<RegisteredApplicationDto>> UpdatePermissionAsync(UpdatePermissionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Result<RegisteredApplicationDto>)CreateApplicationDto());
        }

        public Task<Result<RegisteredApplicationDto>> ChangeLifecycleAsync(ChangeLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Result<RegisteredApplicationDto>)CreateApplicationDto());
        }

        public Task<Result<RegisteredApplicationDto>> TransferOwnershipAsync(TransferOwnershipRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Result<RegisteredApplicationDto>)CreateApplicationDto());
        }

        public Task<Result<RegisteredApplicationDto>> AddMaintainerAsync(AddMaintainerRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Result<RegisteredApplicationDto>)CreateApplicationDto());
        }

        public Task<Result<RegisteredApplicationDto>> RemoveMaintainerAsync(RemoveMaintainerRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Result<RegisteredApplicationDto>)CreateApplicationDto());
        }

        public Task<Result<DeletionImpactDto>> PreviewDeletePermissionAsync(PreviewDeletePermissionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Result<DeletionImpactDto>)new DeletionImpactDto(request.PermissionId, "permission", [], []));
        }

        public Task<Result<DestructiveOperationResultDto>> DeletePermissionAsync(DeletePermissionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Result<DestructiveOperationResultDto>)CreateDestructiveResult());
        }

        public Task<Result<DeletionImpactDto>> PreviewDeleteApplicationAsync(PreviewDeleteApplicationRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Result<DeletionImpactDto>)new DeletionImpactDto(request.ApplicationId, "application", [], []));
        }

        public Task<Result<DestructiveOperationResultDto>> DeleteApplicationAsync(DeleteApplicationRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Result<DestructiveOperationResultDto>)CreateDestructiveResult());
        }

        public Task<Result<RemovedPermissionDetailDto>> UpdateRemovedPermissionReplacementAsync(UpdateRemovedPermissionReplacementRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Result<RemovedPermissionDetailDto>)new RemovedPermissionDetailDto(
                request.PermissionId,
                Guid.NewGuid(),
                "orders-api",
                "orders:write",
                "orders-api:orders:write",
                "Write orders",
                null,
                null,
                DateTimeOffset.UtcNow,
                null,
                DateTimeOffset.UtcNow,
                request.ActorId,
                "Retired.",
                request.ReplacementFullPermissionKey,
                request.ReplacementNote,
                request.ExpectedConcurrencyToken ?? 0));
        }

        public Task<Result<ApplicationPermissionHistoryDto>> ListHistoryAsync(ListHistoryRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Result<ApplicationPermissionHistoryDto>)new ApplicationPermissionHistoryDto([], []));
        }

        public Task<Result<PermissionDiagnosticsDto>> ListDiagnosticsAsync(ListDiagnosticsRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Result<PermissionDiagnosticsDto>)new PermissionDiagnosticsDto([]));
        }

        private static RegisteredApplicationDto CreateApplicationDto()
        {
            return new RegisteredApplicationDto(
                Guid.NewGuid(),
                "orders-api",
                "Orders API",
                "Order permissions",
                "owner-1",
                OwnerType.User.ToString(),
                ApplicationLifecycleStatus.Active.ToString(),
                DateTimeOffset.UtcNow,
                null,
                1,
                [],
                [],
                "1.0.0",
                "1.0.0",
                "https://orders.example.com");
        }

        private static DestructiveOperationResultDto CreateDestructiveResult()
        {
            return new DestructiveOperationResultDto([], [], [], [], MetadataUpdated: false, ManifestVersionAdvanced: false);
        }
    }
}
