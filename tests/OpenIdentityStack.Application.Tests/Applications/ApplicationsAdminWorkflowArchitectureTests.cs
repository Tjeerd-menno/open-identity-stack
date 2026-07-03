namespace OpenIdentityStack.Application.Tests.Applications;

public sealed class ApplicationsAdminWorkflowArchitectureTests
{
    [Fact]
    public void WorkflowKeepsOrchestrationButDoesNotExposePureReadDelegates()
    {
        string workflowSource = ReadSource("src", "OpenIdentityStack.Application", "Applications", "ApplicationsAdminWorkflow.cs");
        string interfaceSource = ReadSource("src", "OpenIdentityStack.Application", "Applications", "IApplicationsAdminWorkflow.cs");

        interfaceSource.ShouldNotContain("Task<Result<PagedResult<ApplicationSummary>>> ListAsync(");
        interfaceSource.ShouldNotContain("Task<Result<ApplicationDetails>> GetDetailsAsync(");
        interfaceSource.ShouldNotContain("Task<Result<IReadOnlyList<ApplicationCredentialDetails>>> ListCredentialsAsync(");
        interfaceSource.ShouldNotContain("Task<Result<IReadOnlyList<ApplicationProfilePolicyDetails>>> ListProfilePoliciesAsync(");

        workflowSource.ShouldNotContain("private readonly ListApplicationsQueryHandler");
        workflowSource.ShouldNotContain("private readonly GetApplicationQueryHandler");
        workflowSource.ShouldNotContain("private readonly ListApplicationCredentialsQueryHandler");
        workflowSource.ShouldNotContain("private readonly ListApplicationProfilePoliciesQueryHandler");
        workflowSource.ShouldNotContain("public Task<Result<PagedResult<ApplicationSummary>>> ListAsync(");
        workflowSource.ShouldNotContain("public Task<Result<ApplicationDetails>> GetDetailsAsync(");
        workflowSource.ShouldNotContain("public Task<Result<IReadOnlyList<ApplicationCredentialDetails>>> ListCredentialsAsync(");
        workflowSource.ShouldNotContain("public Task<Result<IReadOnlyList<ApplicationProfilePolicyDetails>>> ListProfilePoliciesAsync(");
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
