extern alias AppHostProject;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;
using OpenIdentityStack.Testing;
using System.IO;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

[assembly: AssemblyFixture(typeof(ManagementWebAppHostFixture))]
// Tests share one ephemeral database, one browser and one signed-in admin account, so they
// must not run concurrently.
[assembly: Parallelization(Mode = ParallelMode.None)]

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

    // The admin operator the browser signs in as via the real OIDC login flow.
    public const string AdminEmail = "admin@test.com";
    public const string AdminPassword = "Admin123!@456";


    public IDistributedApplicationTestingBuilder? Builder { get; private set; }
    public DistributedApplication? App { get; private set; }
    public HttpClient? ApiClient { get; private set; }
    public string? ManagementWebUrl { get; private set; }
    private IPlaywright? Playwright { get; set; }
    private IBrowser? Browser { get; set; }
    private OpenIdentityStackTestSeeder? TestSeeder { get; set; }

    public async ValueTask InitializeAsync()
    {
        EnsureRequiredAspireParameters();

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

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        EnsureChromiumRuntimeIsAvailable(Playwright);

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
        // Tests authenticate through the real OIDC login flow (see SignInAsync); the app has no
        // mock-auth path to fall back to.

        return context;
    }

    private static async Task ConfigureViteReactRefreshFallbackAsync(IBrowserContext context)
    {
        await context.AddInitScriptAsync(
            "window.$RefreshReg$ = window.$RefreshReg$ || (() => {});" +
            "window.$RefreshSig$ = window.$RefreshSig$ || (() => (type) => type);");
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

        // The browser signs in as this user; give it the seeded super-admin role so the
        // real access token carries permission '*' and every admin surface renders.
        Guid userId = await TestSeeder.CreateTestUserAsync(AdminEmail, "E2E Admin", AdminPassword);
        Guid superAdminRoleId = await TestSeeder.CreateTestRoleAsync("super-admin", "Super Admin");
        await TestSeeder.AssignRoleToUserAsync(userId, superAdminRoleId);

        // Public OAuth client the SPA uses for the authorization-code + PKCE login flow.
        string baseUrl = ManagementWebUrl.TrimEnd('/');
        await TestSeeder.CreatePublicClientAsync(
            "management-web-client",
            "Management Web Client",
            new[] { $"{baseUrl}/auth/callback" },
            new[] { $"{baseUrl}/" });
    }

    /// <summary>
    /// Returns an <see cref="HttpClient"/> bearing the signed-in admin user's real access token
    /// (subject = the admin user's id), pointed at the real API. Use it to seed domain data
    /// through the real admin endpoints. The user subject (not a client id) is required by
    /// surfaces such as application-permission registration, which reject non-user actors.
    /// </summary>
    public async Task<HttpClient> CreateUserApiClientAsync(IPage page)
    {
        if (App is null)
        {
            throw new InvalidOperationException("App is not initialized.");
        }

        string token = await ExtractAccessTokenAsync(page);
        // Bind to the api's HTTP endpoint — the same one the SPA used as its OIDC authority — so the
        // user token's issuer matches what the API derives for the request (otherwise: invalid_token).
        HttpClient client = App.CreateHttpClient("api", "http");
        client.Timeout = RequestTimeout;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Reads the oidc-client-ts access token the SPA stored in session storage after sign-in.</summary>
    private static async Task<string> ExtractAccessTokenAsync(IPage page)
    {
        string? token = await page.EvaluateAsync<string?>(@"() => {
            for (let i = 0; i < sessionStorage.length; i++) {
                const key = sessionStorage.key(i);
                if (key && key.startsWith('oidc.user:')) {
                    try { return JSON.parse(sessionStorage.getItem(key)).access_token; } catch { /* ignore */ }
                }
            }
            return null;
        }");

        return token ?? throw new InvalidOperationException("No oidc.user access token found in session storage after sign-in.");
    }

    /// <summary>
    /// Drives the real OIDC sign-in: starts at the SPA, clicks through to the API login
    /// page, submits the seeded admin credentials, and waits for the authenticated shell.
    /// </summary>
    public async Task SignInAsync(IPage page)
    {
        var consoleMessages = new List<string>();
        page.Console += (_, msg) => consoleMessages.Add($"[{msg.Type}] {msg.Text}");

        await page.GotoAsync(new Uri(new Uri(BaseUrlOrThrow), "/").ToString());

        // Unauthenticated SPA renders its own "Sign in" button which triggers signinRedirect().
        ILocator signInButton = page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true });
        try
        {
            await signInButton.ClickAsync(new LocatorClickOptions { Timeout = 30_000 });
        }
        catch (TimeoutException)
        {
            string url = page.Url;
            string bodyText = await page.InnerTextAsync("body");
            object preamble = await page.EvaluateAsync<object>("() => window.__vite_plugin_react_preamble_installed__ ?? 'undefined'");
            int rootLen = await page.EvaluateAsync<int>("() => (document.getElementById('root')?.innerHTML.length) ?? -1");
            string consoleDump = string.Join("\n  ", consoleMessages.TakeLast(20));
            throw new InvalidOperationException(
                $"SignIn: 'Sign in' button not found.\nURL={url}\npreambleInstalled={preamble}  rootInnerHtmlLen={rootLen}\n--- body innerText ---\n{bodyText}\n--- console ({consoleMessages.Count}) ---\n  {consoleDump}");
        }

        // Now on the API-hosted Razor login page (cross-origin). Submit seeded credentials.
        await page.WaitForURLAsync(new System.Text.RegularExpressions.Regex("/Account/Login", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        await page.Locator("#Email").FillAsync(AdminEmail);
        await page.Locator("#Password").FillAsync(AdminPassword);
        await page.Locator("button[type=submit]").ClickAsync();

        // After code redemption the SPA lands on the authenticated Overview shell.
        await page.GetByRole(AriaRole.Heading, new() { Name = "Overview", Exact = true }).WaitForAsync();
    }

    private string BaseUrlOrThrow =>
        ManagementWebUrl ?? throw new InvalidOperationException("ManagementWeb URL was not initialized.");

    // ----- Typed seeding helpers (users/roles/sessions go through the seeder so accounts
    // are verified/active; other entity types are seeded via the real admin API). -----

    private OpenIdentityStackTestSeeder SeederOrThrow =>
        TestSeeder ?? throw new InvalidOperationException("TestSeeder is not initialized.");

    public async Task<Guid> SeedUserAsync(string email, string displayName, string password, bool disabled = false)
    {
        Guid id = await SeederOrThrow.CreateTestUserAsync(email, displayName, password);
        if (disabled)
        {
            await SeederOrThrow.DisableUserAsync(id);
        }
        return id;
    }

    public Task<Guid> SeedRoleAsync(string name, string? description = null, IReadOnlyList<string>? permissions = null) =>
        SeederOrThrow.CreateTestRoleAsync(name, description, permissions);

    public Task AssignRoleAsync(Guid userId, Guid roleId) =>
        SeederOrThrow.AssignRoleToUserAsync(userId, roleId);

    public Task<Guid> SeedSessionAsync(Guid userId, string ipAddress, string userAgent) =>
        SeederOrThrow.CreateSessionAsync(userId, ipAddress, userAgent);

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

    private static void EnsureRequiredAspireParameters()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(DefaultAdminPasswordParameter)))
        {
            Environment.SetEnvironmentVariable(DefaultAdminPasswordParameter, DefaultAdminPassword);
        }
    }

    private static void EnsureChromiumRuntimeIsAvailable(IPlaywright playwright)
    {
        string executablePath = playwright.Chromium.ExecutablePath;
        if (!File.Exists(executablePath))
        {
            throw new InvalidOperationException(
                "Playwright Chromium runtime was not found for ManagementWeb E2E tests. " +
                "Install browsers using the generated Playwright script (for example: " +
                "'pwsh tests/OpenIdentityStack.ManagementWeb.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium' " +
                "or './tests/OpenIdentityStack.ManagementWeb.E2ETests/bin/Debug/net10.0/playwright.sh install chromium') " +
                "and rerun the tests.");
        }
    }
}
