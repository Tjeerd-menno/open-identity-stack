using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// E2E coverage for the ManagementWeb Audit slice.
/// API responses are route-mocked so this verifies the frontend contract and routing surface.
/// </summary>
public sealed class AuditEntryManagementTests : IAsyncLifetime
{
    private readonly ManagementWebAppHostFixture fixture;
    private readonly List<string> adminRequestUrls = [];
    private IBrowserContext? context;
    private IPage? page;

    public AuditEntryManagementTests(ManagementWebAppHostFixture fixture)
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

        await SetupAuditApiMocksAsync(page);
    }

    public async ValueTask DisposeAsync()
    {
        if (page is not null) await page.CloseAsync();
        if (context is not null) await context.CloseAsync();
    }

    [Fact]
    public async Task OperatorCanFilterAndInspectAuditEntries()
    {
        string baseUrl = fixture.ManagementWebUrl ?? throw new InvalidOperationException("ManagementWeb URL was not initialized.");

        await page!.GotoAsync(new Uri(new Uri(baseUrl), "/audit-entries").ToString());
        await page.GetByRole(AriaRole.Heading, new() { Name = "Audit", Exact = true }).WaitForAsync();
        await page.GetByText("Application.Updated").WaitForAsync();

        await page.GetByLabel(new Regex("User ID", RegexOptions.IgnoreCase)).FillAsync("admin-user");
        await page.GetByLabel(new Regex("^Action$", RegexOptions.IgnoreCase)).FillAsync("Application.Updated");
        await page.GetByLabel(new Regex("Entity type", RegexOptions.IgnoreCase)).FillAsync("Application");
        await page.GetByLabel(new Regex("Entity ID", RegexOptions.IgnoreCase)).FillAsync("orders-web");
        await page.GetByLabel(new Regex("Search audit entries", RegexOptions.IgnoreCase)).FillAsync("redirect");
        Task<IRequest> filteredRequest = page.WaitForRequestAsync(new Regex(@"/api/admin/audit-entries\?.*search=redirect", RegexOptions.IgnoreCase));
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("apply filters", RegexOptions.IgnoreCase) }).ClickAsync();
        await filteredRequest;

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("expand audit entry audit-1", RegexOptions.IgnoreCase) }).ClickAsync();
        ILocator details = page.GetByRole(AriaRole.Region, new() { NameRegex = new Regex("audit entry details", RegexOptions.IgnoreCase) });
        await details.WaitForAsync();
        await details.GetByText("Changed redirect URL").WaitForAsync();
        await details.GetByText("{\"redirect\":\"old\"}").WaitForAsync();
        await details.GetByText("{\"redirect\":\"new\"}").WaitForAsync();

        adminRequestUrls.Any((url) => url.Contains("/api/admin/audit-entries", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/service-accounts", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/clients", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
    }

    private static async Task SetupAuditApiMocksAsync(IPage testPage)
    {
        await testPage.RouteAsync("**/api/admin/**", async route =>
        {
            Uri uri = new(route.Request.Url);
            string path = uri.AbsolutePath;
            string method = route.Request.Method;

            if (method == "GET" && path.Equals("/api/admin/audit-entries", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, AuditEntriesList());
                return;
            }

            await route.FulfillAsync(new() { Status = 404, Body = $"Unexpected E2E route: {method} {path}" });
        });
    }

    private static object AuditEntriesList()
    {
        return new
        {
            items = new[]
            {
                new
                {
                    id = "audit-1",
                    timestamp = "2026-06-01T12:00:00Z",
                    userId = "admin-user",
                    action = "Application.Updated",
                    entityType = "Application",
                    entityId = "orders-web",
                    details = "Changed redirect URL",
                    beforeState = "{\"redirect\":\"old\"}",
                    afterState = "{\"redirect\":\"new\"}"
                }
            },
            totalCount = 1,
            page = 1,
            pageSize = 20,
            totalPages = 1,
            hasPreviousPage = false,
            hasNextPage = false
        };
    }

    private static Task FulfillJsonAsync(IRoute route, object body, int statusCode = 200)
    {
        return route.FulfillAsync(new()
        {
            Status = statusCode,
            ContentType = "application/json",
            Body = System.Text.Json.JsonSerializer.Serialize(body)
        });
    }
}
