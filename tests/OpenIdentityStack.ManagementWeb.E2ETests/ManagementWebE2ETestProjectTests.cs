namespace OpenIdentityStack.ManagementWeb.E2ETests;

public class ManagementWebE2ETestProjectTests
{
    [Fact]
    public void PlaywrightCoverage_ShouldIncludeCompletedDotNetSliceTests()
    {
        string projectDirectory = AppContext.BaseDirectory;
        DirectoryInfo? directory = new(projectDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenIdentityStack.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull();
        string testProjectDirectory = Path.Combine(directory.FullName, "tests", "OpenIdentityStack.ManagementWeb.E2ETests");

        File.Exists(Path.Combine(testProjectDirectory, "AuthContinuityTests.cs")).ShouldBeTrue();
        File.Exists(Path.Combine(testProjectDirectory, "ApplicationManagementTests.cs")).ShouldBeTrue();
        File.Exists(Path.Combine(testProjectDirectory, "UserManagementTests.cs")).ShouldBeTrue();
        File.Exists(Path.Combine(testProjectDirectory, "RoleManagementTests.cs")).ShouldBeTrue();
        File.Exists(Path.Combine(testProjectDirectory, "GroupManagementTests.cs")).ShouldBeTrue();
        File.Exists(Path.Combine(testProjectDirectory, "SessionManagementTests.cs")).ShouldBeTrue();
        File.Exists(Path.Combine(testProjectDirectory, "ProviderManagementTests.cs")).ShouldBeTrue();
        File.Exists(Path.Combine(testProjectDirectory, "SettingsManagementTests.cs")).ShouldBeTrue();
        File.Exists(Path.Combine(testProjectDirectory, "ApplicationPermissionsManagementTests.cs")).ShouldBeTrue();
    }
}
