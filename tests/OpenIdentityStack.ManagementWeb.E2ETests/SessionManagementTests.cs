using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// E2E coverage for the ManagementWeb Sessions slice.
/// API responses are route-mocked so this verifies the frontend contract and routing surface.
/// </summary>
public class SessionManagementTests : IAsyncLifetime
{
    private readonly ManagementWebAppHostFixture fixture;
    private readonly List<string> adminRequestUrls = [];
    private IBrowserContext? context;
    private IPage? page;
    private bool sessionRevoked;
    private bool allUserSessionsRevoked;

    public SessionManagementTests(ManagementWebAppHostFixture fixture)
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

        await SetupSessionsApiMocksAsync(page);
    }

    public async ValueTask DisposeAsync()
    {
        if (page is not null) await page.CloseAsync();
        if (context is not null) await context.CloseAsync();
    }

    [Fact]
    public async Task OperatorCanListInspectAndRevokeSessions()
    {
        string baseUrl = fixture.ManagementWebUrl ?? throw new InvalidOperationException("ManagementWeb URL was not initialized.");

        await page!.GotoAsync(new Uri(new Uri(baseUrl), "/sessions").ToString());
        await page.GetByRole(AriaRole.Heading, new() { Name = "Sessions", Exact = true }).WaitForAsync();
        await page.GetByText("10.0.0.5").WaitForAsync();

        Task<IRequest> searchRequest = page.WaitForRequestAsync(new Regex(@"/api/admin/sessions\?.*search=10\.0\.0\.5", RegexOptions.IgnoreCase));
        await page.GetByLabel(new Regex("Search sessions", RegexOptions.IgnoreCase)).FillAsync("10.0.0.5");
        await searchRequest;

        Task<IRequest> revokedRequest = page.WaitForRequestAsync(new Regex(@"/api/admin/sessions\?.*status=Revoked", RegexOptions.IgnoreCase));
        await page.GetByLabel(new Regex("Filter by status", RegexOptions.IgnoreCase)).SelectOptionAsync(["Revoked"]);
        await revokedRequest;

        await page.GetByLabel(new Regex("Filter by status", RegexOptions.IgnoreCase)).SelectOptionAsync(["Active"]);
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("view session", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Session Details", Exact = true }).WaitForAsync();
        await page.GetByText("Firefox on Windows").WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("revoke all sessions for user", RegexOptions.IgnoreCase) }).ClickAsync();
        Task<IRequest> revokeAllRequest = page.WaitForRequestAsync(new Regex(@"/api/admin/users/user-1/sessions$", RegexOptions.IgnoreCase));
        await page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex("revoke all user sessions", RegexOptions.IgnoreCase) })
            .GetByRole(AriaRole.Button, new() { NameRegex = new Regex("revoke all user sessions", RegexOptions.IgnoreCase) })
            .ClickAsync();
        await revokeAllRequest;
        allUserSessionsRevoked.ShouldBeTrue();

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^revoke session$", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex("revoke session", RegexOptions.IgnoreCase) })
            .GetByRole(AriaRole.Button, new() { NameRegex = new Regex("revoke session", RegexOptions.IgnoreCase) })
            .ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/sessions/?$"));
        sessionRevoked.ShouldBeTrue();

        adminRequestUrls.Any((url) => url.Contains("/api/admin/sessions", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/users/user-1/sessions", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/service-accounts", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/clients", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
    }

    private async Task SetupSessionsApiMocksAsync(IPage testPage)
    {
        await testPage.RouteAsync("**/api/admin/**", async route =>
        {
            Uri uri = new(route.Request.Url);
            string path = uri.AbsolutePath;
            string method = route.Request.Method;

            if (method == "GET" && path.Equals("/api/admin/sessions", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, SessionsList(uri.Query.Contains("status=Revoked", StringComparison.OrdinalIgnoreCase)));
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/sessions/session-1", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, Session());
                return;
            }

            if (method == "DELETE" && path.Equals("/api/admin/sessions/session-1", StringComparison.OrdinalIgnoreCase))
            {
                sessionRevoked = true;
                await route.FulfillAsync(new() { Status = 204 });
                return;
            }

            if (method == "DELETE" && path.Equals("/api/admin/users/user-1/sessions", StringComparison.OrdinalIgnoreCase))
            {
                allUserSessionsRevoked = true;
                await FulfillJsonAsync(route, new
                {
                    revokedCount = 2,
                    revokedAt = "2026-01-02T11:00:00Z"
                });
                return;
            }

            await route.FulfillAsync(new() { Status = 404, Body = $"Unexpected E2E route: {method} {path}" });
        });
    }

    private static object SessionsList(bool revoked)
    {
        object item = revoked
            ? new
            {
                id = "session-2",
                userId = "user-1",
                ipAddress = "10.0.0.7",
                userAgent = "Firefox on Windows",
                status = "Revoked",
                clientCount = 1,
                lastActivityAt = "2026-01-02T10:00:00Z",
                expiresAt = "2026-01-02T12:00:00Z",
                createdAt = "2026-01-02T08:00:00Z"
            }
            : Session();

        return new
        {
            items = new[] { item },
            totalCount = 1,
            page = 1,
            pageSize = 20,
            totalPages = 1
        };
    }

    private static object Session()
    {
        return new
        {
            id = "session-1",
            userId = "user-1",
            ipAddress = "10.0.0.5",
            userAgent = "Firefox on Windows",
            status = "Active",
            clientCount = 2,
            lastActivityAt = "2026-01-02T10:00:00Z",
            expiresAt = "2026-01-02T12:00:00Z",
            createdAt = "2026-01-02T08:00:00Z"
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
