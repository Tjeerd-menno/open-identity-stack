namespace OpenIdentityStack.ManagementWeb.E2ETests;

public class ManagementWebE2ETestProjectTests
{
    [Fact]
    public void PlaywrightCoverage_ShouldIncludeUsersAndDualUiSpecs()
    {
        string projectDirectory = AppContext.BaseDirectory;
        DirectoryInfo? directory = new(projectDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenIdentityStack.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull();
        string testProjectDirectory = Path.Combine(directory.FullName, "tests", "OpenIdentityStack.ManagementWeb.E2ETests");

        File.Exists(Path.Combine(testProjectDirectory, "playwright.config.ts")).ShouldBeTrue();
        File.Exists(Path.Combine(testProjectDirectory, "users.spec.ts")).ShouldBeTrue();
        File.Exists(Path.Combine(testProjectDirectory, "auth-continuity.spec.ts")).ShouldBeTrue();
    }
}
