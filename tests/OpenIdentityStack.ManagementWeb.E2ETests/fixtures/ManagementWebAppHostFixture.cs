extern alias AppHostProject;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using OpenIdentityStack.Testing;
using System.IO;

namespace OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

/// <summary>
/// Shared fixture for Aspire-based E2E tests for the Management Web App.
/// Provides access to API and ManagementWeb resources.
/// Seeds test user and OAuth client for authentication testing.
/// </summary>
public class ManagementWebAppHostFixture : IAsyncLifetime
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private const string DefaultAdminPasswordParameter = "Parameters__default-admin-password";
    private const string DefaultAdminPassword = "Test1234@Test1234";
    
    public IDistributedApplicationTestingBuilder? Builder { get; private set; }
    public DistributedApplication? App { get; private set; }
    public HttpClient? ApiClient { get; private set; }
    public string? ManagementWebUrl { get; private set; }
    private OpenIdentityStackTestSeeder? TestSeeder { get; set; }
    private string? _envLocalPath;

    public async ValueTask InitializeAsync()
    {
        EnsureRequiredAspireParameters();

        // Write .env.local to the ManagementWeb source directory before Vite starts.
        // Vite unconditionally reads .env.local and exposes VITE_* vars as import.meta.env.*,
        // providing a reliable way to activate MockAuthProvider for E2E tests regardless
        // of whether the orchestrator successfully injects VITE_E2E_TEST_MODE via process env.
        _envLocalPath = FindAndWriteEnvLocal();

        // Reference the AppHost project type to ensure it's loaded
        _ = typeof(AppHostProject::Projects.OpenIdentityStack_AppHost);

        Builder = await AspireTestApplication.CreateBuilderAsync<AppHostProject::Projects.OpenIdentityStack_AppHost>(includeManagementWeb: true);
            
        App = await Builder.BuildAsync();
            
        await App.StartAsync();

        // Wait for resources to be healthy
        using CancellationTokenSource cts = new(DefaultTimeout);
        await App.ResourceNotifications.WaitForResourceHealthyAsync("postgres", cts.Token);
        await App.ResourceNotifications.WaitForResourceHealthyAsync("api", cts.Token);

        ApiClient = App.CreateHttpClient("api");
        ApiClient.Timeout = RequestTimeout;
        ManagementWebUrl = await GetManagementWebUrlAsync();
        await AspireTestApplication.WaitForHttpReadyAsync(
            App.CreateHttpClient("managementweb"),
            DefaultTimeout,
            "/@vite/client",
            cts.Token);

        string connectionString = await GetRequiredConnectionStringAsync("openidentitystack");
        TestSeeder = await OpenIdentityStackTestSeeder.CreateAsync(connectionString);

        // Seed test data for authentication tests
        await SeedTestDataAsync();
    }

    private async Task<string> GetManagementWebUrlAsync()
    {
        if (App is null) throw new InvalidOperationException("App is not initialized.");

        // Get the managementweb resource and its endpoint
        HttpClient client = App.CreateHttpClient("managementweb");
        return client.BaseAddress?.ToString() ?? throw new InvalidOperationException("ManagementWeb URL not found");
    }

    /// <summary>
    /// Seeds test user and OAuth client for authentication tests.
    /// </summary>
    private async Task SeedTestDataAsync()
    {
        if (ManagementWebUrl is null || TestSeeder is null)
        {
            throw new InvalidOperationException("TestSeeder or ManagementWebUrl is not initialized.");
        }

        Guid userId = await TestSeeder.CreateTestUserAsync("admin@test.com", "Ada Lovelace", "Admin123!@456");
        Guid roleId = await TestSeeder.CreateTestRoleAsync("operator", "System Operator with user management access");
        await TestSeeder.AssignRoleToUserAsync(userId, roleId);

        // Create public OAuth client for management-web
        string baseUrl = ManagementWebUrl.TrimEnd('/');
        await TestSeeder.CreatePublicClientAsync(
            "management-web-client",
            "Management Web Client",
            new[] { $"{baseUrl}/auth/callback" },
            new[] { $"{baseUrl}/" });
    }

    public async ValueTask DisposeAsync()
    {
        ApiClient?.Dispose();
        if (TestSeeder is not null)
        {
            await TestSeeder.DisposeAsync();
        }
        if (App is not null)
        {
            await App.DisposeAsync();
        }
        // Clean up the .env.local file written for E2E test mode activation.
        if (_envLocalPath is not null && File.Exists(_envLocalPath))
        {
            File.Delete(_envLocalPath);
        }
        GC.SuppressFinalize(this);
    }

    private async Task<string> GetRequiredConnectionStringAsync(string name)
    {
        if (App is null)
        {
            throw new InvalidOperationException("App is not initialized.");
        }

        string? connectionString = await App.GetConnectionStringAsync(name);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{name}' was not found.");
        }

        return connectionString;
    }

    private static string? FindAndWriteEnvLocal()
    {
        // Walk from the test binary directory up to the repository root (contains OpenIdentityStack.slnx).
        string? repoRoot = AppContext.BaseDirectory;
        while (repoRoot is not null && !File.Exists(Path.Combine(repoRoot, "OpenIdentityStack.slnx")))
        {
            repoRoot = Directory.GetParent(repoRoot)?.FullName;
        }

        if (repoRoot is null)
        {
            return null;
        }

        string envLocalPath = Path.Combine(repoRoot, "src", "OpenIdentityStack.ManagementWeb", ".env.local");
        File.WriteAllText(envLocalPath, "VITE_E2E_TEST_MODE=true\n");
        return envLocalPath;
    }

    private static void EnsureRequiredAspireParameters()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(DefaultAdminPasswordParameter)))
        {
            Environment.SetEnvironmentVariable(DefaultAdminPasswordParameter, DefaultAdminPassword);
        }
    }
}
