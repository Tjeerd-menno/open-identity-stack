using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// E2E coverage for authentication settings in the ManagementWeb Identity providers surface.
/// API responses are route-mocked so this verifies the frontend contract and routing surface.
/// </summary>
public class SettingsManagementTests : IAsyncLifetime
{
    private readonly ManagementWebAppHostFixture fixture;
    private readonly List<string> adminRequestUrls = [];
    private IBrowserContext? context;
    private IPage? page;
    private string defaultProviderId = "local";
    private bool isLocalDefault = true;
    private bool localFallbackEnabled = true;

    public SettingsManagementTests(ManagementWebAppHostFixture fixture)
    {
        this.fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        context = await fixture.CreateBrowserContextAsync();
        page = await context.NewPageAsync();
        page.SetDefaultTimeout(60_000);
        page.SetDefaultNavigationTimeout(60_000);
        page.Request += (_, request) =>
        {
            if (request.Url.Contains("/api/admin/", StringComparison.OrdinalIgnoreCase))
            {
                adminRequestUrls.Add(request.Url);
            }
        };

        await SetupSettingsApiMocksAsync(page);
    }

    public async ValueTask DisposeAsync()
    {
        if (page is not null) await page.CloseAsync();
        if (context is not null) await context.CloseAsync();
    }

    [Fact]
    public async Task OperatorCanConfigureAuthenticationSettings()
    {
        string baseUrl = fixture.ManagementWebUrl ?? throw new InvalidOperationException("ManagementWeb URL was not initialized.");

        await page!.GotoAsync(new Uri(new Uri(baseUrl), "/providers/settings").ToString());
        await page.GetByRole(AriaRole.Heading, new() { Name = "Authentication Settings", Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Default Authentication Provider", Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Admin Local Fallback", Exact = true }).WaitForAsync();

        await page.GetByLabel(new Regex("Default Provider", RegexOptions.IgnoreCase)).SelectOptionAsync(["provider-1"]);
        await page.GetByText("Default authentication provider updated successfully.").WaitForAsync();
        defaultProviderId.ShouldBe("provider-1");
        isLocalDefault.ShouldBeFalse();

        Task<IRequest> fallbackRequest = page.WaitForRequestAsync(new Regex(@"/api/admin/authentication-settings/local-fallback$", RegexOptions.IgnoreCase));
        await page.GetByRole(AriaRole.Checkbox, new() { NameRegex = new Regex("Enable local fallback", RegexOptions.IgnoreCase) }).ClickAsync();
        await fallbackRequest;
        await page.GetByText("Local fallback setting updated successfully.").WaitForAsync();
        localFallbackEnabled.ShouldBeFalse();

        adminRequestUrls.Any((url) => url.Contains("/api/admin/authentication-settings", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/service-accounts", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/clients", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
    }

    private async Task SetupSettingsApiMocksAsync(IPage testPage)
    {
        await testPage.RouteAsync("**/api/admin/**", async route =>
        {
            Uri uri = new(route.Request.Url);
            string path = uri.AbsolutePath;
            string method = route.Request.Method;

            if (method == "GET" && path.Equals("/api/admin/authentication-settings", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, Settings());
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/authentication-settings/providers", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, new[]
                {
                    new { id = "local", name = "local", displayName = "Local Accounts", type = "local", isActive = true },
                    new { id = "provider-1", name = "google", displayName = "Google Login", type = "external", isActive = true }
                });
                return;
            }

            if (method == "PUT" && path.Equals("/api/admin/authentication-settings/default-provider", StringComparison.OrdinalIgnoreCase))
            {
                JsonElement body = JsonDocument.Parse(route.Request.PostData ?? "{}").RootElement;
                defaultProviderId = body.GetProperty("providerId").GetString() ?? "local";
                isLocalDefault = defaultProviderId == "local";
                await FulfillJsonAsync(route, Settings());
                return;
            }

            if (method == "PUT" && path.Equals("/api/admin/authentication-settings/local-fallback", StringComparison.OrdinalIgnoreCase))
            {
                JsonElement body = JsonDocument.Parse(route.Request.PostData ?? "{}").RootElement;
                localFallbackEnabled = body.GetProperty("enabled").GetBoolean();
                await FulfillJsonAsync(route, Settings());
                return;
            }

            await route.FulfillAsync(new() { Status = 404, Body = $"Unexpected E2E route: {method} {path}" });
        });
    }

    private object Settings()
    {
        return new
        {
            defaultProviderId,
            isLocalDefault,
            localFallbackEnabled,
            updatedAt = "2026-01-02T00:00:00Z"
        };
    }

    private static Task FulfillJsonAsync(IRoute route, object body, int statusCode = 200)
    {
        return route.FulfillAsync(new()
        {
            Status = statusCode,
            ContentType = "application/json",
            Body = JsonSerializer.Serialize(body)
        });
    }
}
