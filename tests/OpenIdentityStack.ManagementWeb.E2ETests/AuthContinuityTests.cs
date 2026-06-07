using System.Text.Json;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

public class AuthContinuityTests : IAsyncLifetime
{
    private readonly ManagementWebAppHostFixture fixture;
    private IBrowserContext? context;
    private IPage? page;

    public AuthContinuityTests(ManagementWebAppHostFixture fixture)
    {
        this.fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        context = await fixture.CreateBrowserContextAsync();
        page = await context.NewPageAsync();
        page.SetDefaultTimeout(60_000);
        page.SetDefaultNavigationTimeout(60_000);
        await SetupApiMocksAsync(page);
    }

    public async ValueTask DisposeAsync()
    {
        if (page is not null) await page.CloseAsync();
        if (context is not null) await context.CloseAsync();
    }

    [Fact]
    public async Task ManagementWebRemainsReachableDuringDualUiRollout()
    {
        string baseUrl = fixture.ManagementWebUrl ?? throw new InvalidOperationException("ManagementWeb URL was not initialized.");

        await page!.GotoAsync(baseUrl);
        await page.GetByText("IAM Console", new() { Exact = true }).WaitForAsync();

        await page.GotoAsync("about:blank");

        await page.GotoAsync(baseUrl);
        await page.GetByRole(AriaRole.Heading, new() { Name = "Users", Exact = true }).WaitForAsync();
    }

    private static async Task SetupApiMocksAsync(IPage testPage)
    {
        await testPage.RouteAsync("**/api/admin/**", async route =>
        {
            Uri uri = new(route.Request.Url);
            string path = uri.AbsolutePath;
            string method = route.Request.Method;

            if (path.EndsWith("/roles", StringComparison.OrdinalIgnoreCase) && path.Contains("/users/", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, new { userId = "user-test-1", roles = Array.Empty<object>() });
                return;
            }

            if (path.StartsWith("/api/admin/roles", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, new
                {
                    items = new[]
                    {
                        new
                        {
                            id = "role-test-1",
                            name = "operator",
                            displayName = "Operator",
                            isSystemRole = false,
                            isActive = true
                        }
                    },
                    totalCount = 1,
                    page = 1,
                    pageSize = 100,
                    totalPages = 1
                });
                return;
            }

            if (path.StartsWith("/api/admin/users", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, new
                {
                    items = new[]
                    {
                        new
                        {
                            id = "user-test-1",
                            email = "admin@test.com",
                            displayName = "Ada Lovelace",
                            status = "Active",
                            createdAt = "2024-01-01T00:00:00Z",
                            mfaEnabled = false,
                            lastLoginAt = (string?)null,
                            modifiedAt = (string?)null,
                            profile = new { }
                        }
                    },
                    totalCount = 1,
                    page = 1,
                    pageSize = 20,
                    totalPages = 1
                });
                return;
            }

            await route.ContinueAsync();
        });
    }

    private static Task FulfillJsonAsync(IRoute route, object body)
    {
        return route.FulfillAsync(new()
        {
            Status = 200,
            ContentType = "application/json",
            Body = JsonSerializer.Serialize(body)
        });
    }
}
