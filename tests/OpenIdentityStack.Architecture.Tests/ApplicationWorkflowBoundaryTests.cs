using System.Text.RegularExpressions;

namespace OpenIdentityStack.Architecture.Tests;

/// <summary>
/// Api application code names the existing workflows. It does not inject lifecycle or maintenance use cases beside them.
/// At 2b17b7ed2d06b54f96a8c4ab135930a06f3693b7, ApplicationPermissionsApi injects ApplicationPermissionMaintenanceUseCases.
/// </summary>
public sealed class ApplicationWorkflowBoundaryTests
{
    private static readonly Regex ForbiddenName = new(
        @"\b(ApplicationLifecycleUseCases|ApplicationCredentialUseCases|ApplicationPermissionMaintenanceUseCases)\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [Fact]
    public void ApiApplicationCode_NamesTheExistingWorkflowsOnly()
    {
        string repoRoot = FindRepositoryRoot();
        string[] files =
        [
            Path.Combine(repoRoot, "src", "OpenIdentityStack.Api", "Applications", "ApplicationsApi.cs"),
            Path.Combine(repoRoot, "src", "OpenIdentityStack.Api", "Admin", "ApplicationPermissionsApi.cs"),
        ];
        var hits = new List<string>();
        foreach (string file in files)
        {
            string[] lines = File.ReadAllLines(file);
            string relativePath = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
            for (int index = 0; index < lines.Length; index++)
            {
                foreach (Match match in ForbiddenName.Matches(lines[index]))
                {
                    hits.Add($"{relativePath}:{index + 1}: {match.Value}");
                }
            }
        }

        hits.Sort(StringComparer.Ordinal);
        hits.ShouldBeEmpty(
            "Api application code names the existing workflows. A lifecycle or maintenance use case in those files is a second path."
            + Environment.NewLine
            + string.Join(Environment.NewLine, hits));

        string applicationsApi = File.ReadAllText(files[0]);
        string permissionsApi = File.ReadAllText(files[1]);
        applicationsApi.ShouldContain("IApplicationsAdminWorkflow");
        permissionsApi.ShouldContain("IApplicationPermissionRegistryWorkflow");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenIdentityStack.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
