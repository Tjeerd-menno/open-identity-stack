extern alias AppHostProject;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;
using OpenIdentityStack.AdminWeb.E2ETests.Fixtures;
using OpenIdentityStack.Testing;

[assembly: AssemblyFixture(typeof(AdminWebAppHostFixture))]

namespace OpenIdentityStack.AdminWeb.E2ETests.Fixtures;

/// <summary>
/// Shared fixture for Aspire-based E2E tests for the Admin Web App.
/// Provides access to both the API and AdminWeb resources.
/// Seeds test user and OAuth client for authentication testing.
/// </summary>
public class AdminWebAppHostFixture : IAsyncLifetime
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    
    public IDistributedApplicationTestingBuilder? Builder { get; private set; }
    public DistributedApplication? App { get; private set; }
    public HttpClient? ApiClient { get; private set; }
    public string? AdminWebUrl { get; private set; }
    private IPlaywright? Playwright { get; set; }
    private IBrowser? Browser { get; set; }
    private OpenIdentityStackTestSeeder? TestSeeder { get; set; }

    public async ValueTask InitializeAsync()
    {
        // Reference the AppHost project type to ensure it's loaded
        _ = typeof(AppHostProject::Projects.OpenIdentityStack_AppHost);

        Builder = await AspireTestApplication.CreateBuilderAsync<AppHostProject::Projects.OpenIdentityStack_AppHost>(includeAdminWeb: true);
            
        App = await Builder.BuildAsync();
            
        await App.StartAsync();

        // Wait for resources to be healthy
        using var cts = new CancellationTokenSource(DefaultTimeout);
        await App.ResourceNotifications.WaitForResourceHealthyAsync("postgres", cts.Token);
        await App.ResourceNotifications.WaitForResourceHealthyAsync("api", cts.Token);

        ApiClient = App.CreateHttpClient("api");
        ApiClient.Timeout = RequestTimeout;
        AdminWebUrl = await GetAdminWebUrlAsync();
        await AspireTestApplication.WaitForHttpReadyAsync(
            App.CreateHttpClient("adminweb"),
            DefaultTimeout,
            "/@vite/client",
            cts.Token);

        string connectionString = await GetRequiredConnectionStringAsync("openidentitystack");
        TestSeeder = await OpenIdentityStackTestSeeder.CreateAsync(connectionString);

        // Seed test data for authentication tests
        await SeedTestDataAsync();

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task<IBrowserContext> CreateBrowserContextAsync()
    {
        if (Browser is null)
        {
            throw new InvalidOperationException("Browser is not initialized.");
        }

        IBrowserContext context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });
        await ConfigureViteReactRefreshFallbackAsync(context);

        return context;
    }

    private static async Task ConfigureViteReactRefreshFallbackAsync(IBrowserContext context)
    {
        await context.AddInitScriptAsync(
            "window.$RefreshReg$ = window.$RefreshReg$ || (() => {});" +
            "window.$RefreshSig$ = window.$RefreshSig$ || (() => (type) => type);");
    }

    private async Task<string> GetAdminWebUrlAsync()
    {
        if (App is null) throw new InvalidOperationException("App is not initialized.");

        // Get the adminweb resource and its endpoint
        HttpClient client = App.CreateHttpClient("adminweb");
        return client.BaseAddress?.ToString() ?? throw new InvalidOperationException("AdminWeb URL not found");
    }

    /// <summary>
    /// Seeds test user and OAuth client for authentication tests.
    /// </summary>
    private async Task SeedTestDataAsync()
    {
        if (AdminWebUrl is null || TestSeeder is null)
        {
            throw new InvalidOperationException("TestSeeder or AdminWebUrl is not initialized.");
        }

        Guid userId = await TestSeeder.CreateTestUserAsync("admin@test.com", "Test Admin", "Admin123!@456");
        Guid roleId = await TestSeeder.CreateTestRoleAsync("super-admin", "Super Administrator with full access");
        await TestSeeder.AssignRoleToUserAsync(userId, roleId);

        // Create public OAuth client for admin-web
        string baseUrl = AdminWebUrl.TrimEnd('/');
        await TestSeeder.CreatePublicClientAsync(
            "admin-web-client",
            "Admin Web Client",
            new[] { $"{baseUrl}/auth/callback" },
            new[] { $"{baseUrl}/login" });
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }
        Playwright?.Dispose();
        ApiClient?.Dispose();
        if (TestSeeder is not null)
        {
            await TestSeeder.DisposeAsync();
        }
        if (App is not null)
        {
            await App.DisposeAsync();
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
}
