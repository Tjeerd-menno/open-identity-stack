namespace OpenIdentityStack.Api.Tests.Admin.Applications;

public sealed class ApplicationsApiArchitectureTests
{
    [Fact]
    public void ReadEndpointsDependOnQueryHandlersWhileCommandEndpointsDependOnWorkflow()
    {
        string source = ReadApplicationsApiSource();

        source.ShouldContain("[FromServices] IListApplicationsQueryHandler listApplicationsQueryHandler");
        source.ShouldContain("[FromServices] IGetApplicationQueryHandler getApplicationQueryHandler");
        source.ShouldContain("[FromServices] IListApplicationCredentialsQueryHandler listApplicationCredentialsQueryHandler");
        source.ShouldContain("[FromServices] IListApplicationProfilePoliciesQueryHandler listApplicationProfilePoliciesQueryHandler");
        source.ShouldContain("[FromServices] IApplicationsAdminWorkflow applicationsAdminWorkflow");

        source.ShouldNotContain("[FromServices] IApplicationsAdminWorkflow applicationsAdminWorkflow,\n        [FromQuery] int page = 1");
        source.ShouldNotContain("[FromServices] IApplicationsAdminWorkflow applicationsAdminWorkflow,\n        CancellationToken cancellationToken)\n    {\n        Result<IReadOnlyList<ApplicationProfilePolicyDetails>> result =\n            await applicationsAdminWorkflow.ListProfilePoliciesAsync");
        source.ShouldNotContain("[FromServices] IApplicationsAdminWorkflow applicationsAdminWorkflow,\n        Guid id,\n        CancellationToken cancellationToken)\n    {\n        return await GetApplicationResponseAsync(applicationsAdminWorkflow, id, cancellationToken);");
        source.ShouldNotContain("[FromServices] IApplicationsAdminWorkflow applicationsAdminWorkflow,\n        Guid id,\n        CancellationToken cancellationToken)\n    {\n        Result<IReadOnlyList<ApplicationCredentialDetails>> result = await applicationsAdminWorkflow.ListCredentialsAsync");
    }

    private static string ReadApplicationsApiSource()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string sourcePath = Path.Combine(directory.FullName, "src", "OpenIdentityStack.Api", "Applications", "ApplicationsApi.cs");
            if (File.Exists(sourcePath))
            {
                return File.ReadAllText(sourcePath);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate ApplicationsApi.cs from the test output directory.");
    }
}
