namespace OpenIdentityStack.Application.Tests.ApplicationPermissions;

public sealed class ApplicationPermissionRegistryWorkflowArchitectureTests
{
    [Fact]
    public void ApplicationLayerDoesNotExposeShallowRegistryUseCaseInterfaces()
    {
        string workflowSource = ReadSource("src", "OpenIdentityStack.Application", "ApplicationPermissions", "ApplicationPermissionRegistryWorkflow.cs");
        string interfaceSource = ReadSource("src", "OpenIdentityStack.Application", "ApplicationPermissions", "IApplicationPermissionRegistryWorkflow.cs");
        string maintenanceSource = ReadSource("src", "OpenIdentityStack.Application", "ApplicationPermissions", "Commands", "ApplicationPermissionMaintenanceUseCases.cs");
        string registrationSource = ReadSource("src", "OpenIdentityStack.Application", "ApplicationPermissions", "Commands", "RegisterApplicationUseCase.cs");
        string dependencyInjectionSource = ReadSource("src", "OpenIdentityStack.Application", "DependencyInjection.cs");

        maintenanceSource.ShouldNotContain("public interface IUpdateRegisteredApplicationUseCase");
        maintenanceSource.ShouldNotContain("public interface IAddApplicationPermissionUseCase");
        maintenanceSource.ShouldNotContain("public interface IUpdateApplicationPermissionUseCase");
        maintenanceSource.ShouldNotContain("public interface IDeleteApplicationPermissionUseCase");
        maintenanceSource.ShouldNotContain("public interface IPreviewApplicationPermissionDeletionImpactUseCase");
        maintenanceSource.ShouldNotContain("public interface IDeleteRegisteredApplicationUseCase");
        maintenanceSource.ShouldNotContain("public interface IPreviewRegisteredApplicationDeletionImpactUseCase");
        maintenanceSource.ShouldNotContain("public interface IChangeRegisteredApplicationLifecycleUseCase");
        maintenanceSource.ShouldNotContain("public interface ITransferRegisteredApplicationOwnershipUseCase");
        maintenanceSource.ShouldNotContain("public interface IAddDelegatedMaintainerUseCase");
        maintenanceSource.ShouldNotContain("public interface IRemoveDelegatedMaintainerUseCase");
        maintenanceSource.ShouldNotContain("public interface IUpdateRemovedPermissionReplacementUseCase");
        maintenanceSource.ShouldNotContain("public interface IListApplicationPermissionHistoryQueryHandler");
        maintenanceSource.ShouldNotContain("public interface IListApplicationPermissionDiagnosticsQueryHandler");
        registrationSource.ShouldNotContain("public interface IRegisterApplicationUseCase");
        registrationSource.ShouldNotContain("public interface IRegisterPermissionManifestUseCase");

        dependencyInjectionSource.ShouldNotContain("IUpdateRegisteredApplicationUseCase");
        dependencyInjectionSource.ShouldNotContain("IAddApplicationPermissionUseCase");
        dependencyInjectionSource.ShouldNotContain("IUpdateApplicationPermissionUseCase");
        dependencyInjectionSource.ShouldNotContain("IDeleteApplicationPermissionUseCase");
        dependencyInjectionSource.ShouldNotContain("IPreviewApplicationPermissionDeletionImpactUseCase");
        dependencyInjectionSource.ShouldNotContain("IDeleteRegisteredApplicationUseCase");
        dependencyInjectionSource.ShouldNotContain("IPreviewRegisteredApplicationDeletionImpactUseCase");
        dependencyInjectionSource.ShouldNotContain("IChangeRegisteredApplicationLifecycleUseCase");
        dependencyInjectionSource.ShouldNotContain("ITransferRegisteredApplicationOwnershipUseCase");
        dependencyInjectionSource.ShouldNotContain("IAddDelegatedMaintainerUseCase");
        dependencyInjectionSource.ShouldNotContain("IRemoveDelegatedMaintainerUseCase");
        dependencyInjectionSource.ShouldNotContain("IUpdateRemovedPermissionReplacementUseCase");
        dependencyInjectionSource.ShouldNotContain("IListApplicationPermissionHistoryQueryHandler");
        dependencyInjectionSource.ShouldNotContain("IListApplicationPermissionDiagnosticsQueryHandler");
        dependencyInjectionSource.ShouldNotContain("IRegisterApplicationUseCase");
        dependencyInjectionSource.ShouldNotContain("IRegisterPermissionManifestUseCase");
        interfaceSource.ShouldNotContain("Task<Result<ApplicationPermissionHistoryDto>> ListHistoryAsync(");
        interfaceSource.ShouldNotContain("Task<Result<PermissionDiagnosticsDto>> ListDiagnosticsAsync(");
        workflowSource.ShouldNotContain("public async Task<Result<ApplicationPermissionHistoryDto>> ListHistoryAsync(");
        workflowSource.ShouldNotContain("public async Task<Result<PermissionDiagnosticsDto>> ListDiagnosticsAsync(");
    }

    private static string ReadSource(params string[] pathParts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string sourcePath = Path.Combine([directory.FullName, .. pathParts]);
            if (File.Exists(sourcePath))
            {
                return File.ReadAllText(sourcePath);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate source file '{string.Join('/', pathParts)}' from the test output directory.");
    }
}
