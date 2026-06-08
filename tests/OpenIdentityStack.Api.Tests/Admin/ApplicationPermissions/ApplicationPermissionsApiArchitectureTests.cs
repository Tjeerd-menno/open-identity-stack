namespace OpenIdentityStack.Api.Tests.Admin.ApplicationPermissions;

public sealed class ApplicationPermissionsApiArchitectureTests
{
    [Fact]
    public void RegistryCommandEndpointsDependOnWorkflowInsteadOfShallowUseCaseInterfaces()
    {
        string source = ReadApplicationPermissionsApiSource();

        source.ShouldContain("[FromServices] IApplicationPermissionRegistryWorkflow workflow");
        source.ShouldNotContain("[FromServices] ApplicationPermissionManifestUseCases");
        source.ShouldNotContain("[FromServices] IUpdateRegisteredApplicationUseCase");
        source.ShouldNotContain("[FromServices] IAddApplicationPermissionUseCase");
        source.ShouldNotContain("[FromServices] IUpdateApplicationPermissionUseCase");
        source.ShouldNotContain("[FromServices] IDeleteApplicationPermissionUseCase");
        source.ShouldNotContain("[FromServices] IPreviewApplicationPermissionDeletionImpactUseCase");
        source.ShouldNotContain("[FromServices] IDeleteRegisteredApplicationUseCase");
        source.ShouldNotContain("[FromServices] IPreviewRegisteredApplicationDeletionImpactUseCase");
        source.ShouldNotContain("[FromServices] IChangeRegisteredApplicationLifecycleUseCase");
        source.ShouldNotContain("[FromServices] ITransferRegisteredApplicationOwnershipUseCase");
        source.ShouldNotContain("[FromServices] IAddDelegatedMaintainerUseCase");
        source.ShouldNotContain("[FromServices] IRemoveDelegatedMaintainerUseCase");
        source.ShouldNotContain("[FromServices] IUpdateRemovedPermissionReplacementUseCase");
        source.ShouldNotContain("[FromServices] IListApplicationPermissionHistoryQueryHandler");
        source.ShouldNotContain("[FromServices] IListApplicationPermissionDiagnosticsQueryHandler");
    }

    private static string ReadApplicationPermissionsApiSource()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string sourcePath = Path.Combine(directory.FullName, "src", "OpenIdentityStack.Api", "Admin", "ApplicationPermissionsApi.cs");
            if (File.Exists(sourcePath))
            {
                return File.ReadAllText(sourcePath);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate ApplicationPermissionsApi.cs from the test output directory.");
    }
}
