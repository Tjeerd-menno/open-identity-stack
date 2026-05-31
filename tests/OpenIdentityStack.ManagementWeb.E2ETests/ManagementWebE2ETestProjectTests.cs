using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;
using System.Diagnostics;
using Aspire.Hosting.Testing;

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

    [Fact]
    public async Task PlaywrightTests_ShouldExecuteAndPass_WithRunningAspireStack()
    {
        // Create and initialize the fixture
        ManagementWebAppHostFixture fixture = new();
        await fixture.InitializeAsync();

        try
        {
            fixture.ManagementWebUrl.ShouldNotBeNullOrEmpty();

            // Discover the test project directory
            string projectDirectory = AppContext.BaseDirectory;
            DirectoryInfo? directory = new(projectDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenIdentityStack.slnx")))
            {
                directory = directory.Parent;
            }

            directory.ShouldNotBeNull();
            string testProjectDirectory = Path.Combine(directory.FullName, "tests", "OpenIdentityStack.ManagementWeb.E2ETests");
            Directory.Exists(testProjectDirectory).ShouldBeTrue();

            await EnsurePlaywrightDependenciesAsync(testProjectDirectory);

            // Set environment variables for Playwright config to discover running Management Web instance
            string originalManagementWebUrl = Environment.GetEnvironmentVariable("MANAGEMENT_WEB_BASE_URL") ?? string.Empty;
            string originalAdminWebUrl = Environment.GetEnvironmentVariable("ADMIN_WEB_BASE_URL") ?? string.Empty;

            try
            {
                Environment.SetEnvironmentVariable("MANAGEMENT_WEB_BASE_URL", fixture.ManagementWebUrl.TrimEnd('/'));
                
                // Try to get Admin Web URL from fixture's app if available, otherwise use default
                string adminWebUrl = "http://localhost:5175";
                try
                {
                    if (fixture.App is not null)
                    {
                        using HttpClient client = fixture.App.CreateHttpClient("adminweb");
                        adminWebUrl = client.BaseAddress?.ToString().TrimEnd('/') ?? adminWebUrl;
                    }
                }
                catch
                {
                    // If Admin Web is not running, use the default URL
                }
                Environment.SetEnvironmentVariable("ADMIN_WEB_BASE_URL", adminWebUrl);

                // Run npx playwright test from the test project directory
                ProcessStartInfo processInfo = new()
                {
                    FileName = GetPlaywrightCommand(testProjectDirectory),
                    Arguments = "test",
                    WorkingDirectory = testProjectDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                processInfo.Environment["MANAGEMENT_WEB_BASE_URL"] = fixture.ManagementWebUrl.TrimEnd('/');
                processInfo.Environment["ADMIN_WEB_BASE_URL"] = adminWebUrl;

                using var process = System.Diagnostics.Process.Start(processInfo);
                process.ShouldNotBeNull();

                string standardOutput = await process.StandardOutput.ReadToEndAsync();
                string standardError = await process.StandardError.ReadToEndAsync();
                
                process.WaitForExit();

                // Assemble comprehensive error message if tests fail
                string errorMessage = $"""
                    Playwright tests failed with exit code {process.ExitCode}.
                    
                    STDOUT:
                    {standardOutput}
                    
                    STDERR:
                    {standardError}
                    
                    Management Web URL: {fixture.ManagementWebUrl}
                    Admin Web URL: {adminWebUrl}
                    """;

                process.ExitCode.ShouldBe(0, errorMessage);
            }
            finally
            {
                // Restore original environment variables
                if (string.IsNullOrEmpty(originalManagementWebUrl))
                    Environment.SetEnvironmentVariable("MANAGEMENT_WEB_BASE_URL", null);
                else
                    Environment.SetEnvironmentVariable("MANAGEMENT_WEB_BASE_URL", originalManagementWebUrl);

                if (string.IsNullOrEmpty(originalAdminWebUrl))
                    Environment.SetEnvironmentVariable("ADMIN_WEB_BASE_URL", null);
                else
                    Environment.SetEnvironmentVariable("ADMIN_WEB_BASE_URL", originalAdminWebUrl);
            }
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    private static string ResolveNodeCommand(string command)
    {
        return OperatingSystem.IsWindows() ? $"{command}.cmd" : command;
    }

    private static async Task EnsurePlaywrightDependenciesAsync(string testProjectDirectory)
    {
        if (File.Exists(GetPlaywrightCommand(testProjectDirectory)))
        {
            return;
        }

        ProcessStartInfo npmInstallInfo = new()
        {
            FileName = ResolveNodeCommand("npm"),
            Arguments = "ci",
            WorkingDirectory = testProjectDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var npmProcess = System.Diagnostics.Process.Start(npmInstallInfo);
        npmProcess.ShouldNotBeNull();
        string standardOutput = await npmProcess.StandardOutput.ReadToEndAsync();
        string standardError = await npmProcess.StandardError.ReadToEndAsync();
        await npmProcess.WaitForExitAsync();

        npmProcess.ExitCode.ShouldBe(0, $"""
            npm ci failed.

            STDOUT:
            {standardOutput}

            STDERR:
            {standardError}
            """);
    }

    private static string GetPlaywrightCommand(string testProjectDirectory)
    {
        string binDirectory = Path.Combine(testProjectDirectory, "node_modules", ".bin");
        return Path.Combine(binDirectory, OperatingSystem.IsWindows() ? "playwright.cmd" : "playwright");
    }
}
